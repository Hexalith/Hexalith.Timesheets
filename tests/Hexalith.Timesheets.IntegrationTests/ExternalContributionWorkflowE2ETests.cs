using System.Text.Json;

using Hexalith.Timesheets.Contracts.Commands.ExternalContributions;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections;
using Hexalith.Timesheets.Projections.TimeEntries;
using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.TimeEntries;

using Shouldly;

namespace Hexalith.Timesheets.IntegrationTests;

public sealed class ExternalContributionWorkflowE2ETests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task External_contribution_record_confirm_and_read_workflow_discloses_confirmation_as_evidence_not_approval()
    {
        AllowAllAccessGuard accessGuard = new();
        ExternalContributionCommandService service = new(
            new TimeEntryCommandService(accessGuard),
            new TimeEntrySubmissionCommandService(accessGuard),
            accessGuard,
            ExternalContributionPolicyOptions.Default);

        ExternalContributionCommandResult submitResult = await service.SubmitAsync(
            Context(),
            SubmitCommand(),
            null,
            FreshCatalog(),
            SubmittedAtUtc(),
            TestContext.Current.CancellationToken);

        submitResult.RecordResult.WasDispatched.ShouldBeTrue();
        submitResult.SubmissionResult.ShouldBeNull();
        TimeEntryRecorded recorded = submitResult.RecordResult.DomainResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimeEntryRecorded>();

        TimeEntryState state = new();
        state.Apply(recorded);

        TimeEntryConfirmationCommandResult confirmResult = await service.ConfirmAsync(
            Context(),
            ConfirmCommand(),
            state,
            ConfirmedAtUtc(),
            TestContext.Current.CancellationToken);

        confirmResult.WasDispatched.ShouldBeTrue();
        TimeEntryContributorConfirmed confirmed = confirmResult.DomainResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimeEntryContributorConfirmed>();

        TimeEntryEvidenceReadModel projected = new TimeEntryEvidenceProjection().Project(
            "tenant-1",
            TimeEntryId(),
            [
                new("message-2", 2, confirmed),
                new("message-1", 1, recorded),
                new("message-2", 2, confirmed)
            ],
            new("tenant-1", TimeEntryEvidenceProjection.ProjectionName, 2, ProjectionFreshness.Fresh))
            .ShouldNotBeNull();

        TimeEntryEvidenceQueryResult queryResult = await new TimeEntryEvidenceQueryService(
            accessGuard,
            new FixedProjectionReader(projected),
            new FixedDisplayHydrator()).ReadAsync(
                Context(),
                TimeEntryId(),
                TestContext.Current.CancellationToken);

        queryResult.WasDisclosed.ShouldBeTrue();
        TimeEntryEvidenceReadModel evidence = queryResult.Evidence.ShouldNotBeNull();
        evidence.ContributorCategory.ShouldBe(ContributorCategory.ExternalContributor);
        evidence.ExternalSource.ShouldBe(new ExternalContributionSource("supplier-api", "submit-1"));
        evidence.ContributorConfirmation.ShouldNotBeNull();
        evidence.ContributorConfirmation.Contributor.ShouldBe(Contributor());
        evidence.ContributorConfirmation.Source.ShouldBe(new ExternalContributionSource("supplier-api", "confirm-1"));
        evidence.ApprovalState.ShouldBe(TimeEntryApprovalState.Draft);
        evidence.ApprovalDecision.ShouldBeNull();
        evidence.LockEvidence.LockState.ShouldBe(TimeEntryLockState.Unlocked);
        evidence.EventLineage.Select(static item => item.EventName)
            .ShouldBe([nameof(TimeEntryRecorded), nameof(TimeEntryContributorConfirmed)]);
        evidence.DisplayHydration.Contributor.Label.ShouldBe("External contributor");
        evidence.DisplayHydration.Target.Label.ShouldBe("Project");
        accessGuard.Requests.Select(static request => request.Operation).ShouldBe(
        [
            TimesheetsOperation.Command,
            TimesheetsOperation.Confirmation,
            TimesheetsOperation.ProjectionRead,
            TimesheetsOperation.ProjectionRead
        ]);

        string json = JsonSerializer.Serialize(evidence, JsonOptions);

        json.ShouldContain("\"contributorCategory\":\"ExternalContributor\"");
        json.ShouldContain("\"sourceSystem\":\"supplier-api\"");
        json.ShouldContain("\"approvalState\":\"Draft\"");
        json.ShouldNotContain("\"approver\"");
        json.ShouldNotContain("\"token\"");
        json.ShouldNotContain("\"command\"");
    }

    private static SubmitExternalTimeEntry SubmitCommand()
        => new(
            TimeEntryId(),
            TimeEntryTargetReference.ForProject(Project()),
            Contributor(),
            ActivityId(),
            new DateOnly(2026, 6, 19),
            60,
            BillableState.Billable,
            new ExternalContributionSource("supplier-api", "submit-1"));

    private static ConfirmExternalTimeEntry ConfirmCommand()
        => new(
            TimeEntryId(),
            Contributor(),
            new ExternalContributionSource("supplier-api", "confirm-1"));

    private static ActivityTypeCatalogReadModel FreshCatalog()
        => new(
            [
                new(
                    ActivityId(),
                    ActivityTypeScope.Tenant,
                    null,
                    "Delivery",
                    true,
                    BillableState.Billable)
            ],
            ProjectionFreshnessMetadata.Fresh);

    private static TimesheetsRequestContext Context()
        => new(
            new TenantReference("tenant-1"),
            new PartyReference("operator-1"),
            "correlation-1");

    private static DateTimeOffset SubmittedAtUtc() => new(2026, 6, 19, 12, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset ConfirmedAtUtc() => new(2026, 6, 19, 12, 30, 0, TimeSpan.Zero);

    private static ProjectReference Project() => new("project-1");

    private static PartyReference Contributor() => new("party-1");

    private static ActivityTypeId ActivityId() => new("activity-type-1");

    private static TimeEntryId TimeEntryId() => new("time-entry-1");

    private sealed class AllowAllAccessGuard : ITimesheetsAccessGuard
    {
        public List<TimesheetsAuthorizationRequest> Requests { get; } = [];

        public ValueTask<TimesheetsAuthorizationDecision> AuthorizeAsync(
            TimesheetsAuthorizationRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);

            return ValueTask.FromResult(TimesheetsAuthorizationDecision.Allowed());
        }

        public async ValueTask<TimesheetsAuthorizationDecision> ExecuteIfAuthorizedAsync(
            TimesheetsAuthorizationRequest request,
            Func<CancellationToken, ValueTask> trustedWork,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            await trustedWork(cancellationToken).ConfigureAwait(false);

            return TimesheetsAuthorizationDecision.Allowed();
        }

        public ValueTask<TimesheetsUiActionPolicyOutcome> EvaluateUiActionAsync(
            TimesheetsAuthorizationRequest request,
            TimesheetsUiActionVisibility deniedVisibility,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);

            return ValueTask.FromResult(TimesheetsUiActionPolicyOutcome.Allowed(
                request.UiAction ?? TimesheetsUiAction.Capture));
        }
    }

    private sealed class FixedProjectionReader : ITimeEntryEvidenceProjectionReader
    {
        private readonly TimeEntryEvidenceReadModel _model;

        public FixedProjectionReader(TimeEntryEvidenceReadModel model)
        {
            ArgumentNullException.ThrowIfNull(model);
            _model = model;
        }

        public ValueTask<TimeEntryEvidenceReadModel?> ReadAsync(
            TimesheetsRequestContext context,
            TimeEntryId timeEntryId,
            CancellationToken cancellationToken)
            => ValueTask.FromResult<TimeEntryEvidenceReadModel?>(_model);
    }

    private sealed class FixedDisplayHydrator : ITimeEntryDisplayHydrator
    {
        public ValueTask<TimeEntryDisplayHydration> HydrateAsync(
            TimesheetsRequestContext context,
            TimeEntryEvidenceReadModel evidence,
            CancellationToken cancellationToken)
            => ValueTask.FromResult(new TimeEntryDisplayHydration(
                TimeEntryHydratedDisplayLabel.Fresh("External contributor"),
                TimeEntryHydratedDisplayLabel.Fresh("Project"),
                TimeEntryHydratedDisplayLabel.Fresh("Delivery")));
    }
}
