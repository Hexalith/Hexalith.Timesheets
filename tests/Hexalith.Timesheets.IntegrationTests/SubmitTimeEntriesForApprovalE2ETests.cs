using System.Text.Json;

using Hexalith.Timesheets.Contracts.Commands.TimeEntries;
using Hexalith.Timesheets.Contracts.Events.Rejections;
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

public sealed class SubmitTimeEntriesForApprovalE2ETests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Draft_time_entry_submission_workflow_records_submits_projects_and_discloses_submitted_evidence()
    {
        AllowAllAccessGuard accessGuard = new();
        TimeEntryCommandService recordService = new(accessGuard);
        TimeEntrySubmissionCommandService submissionService = new(accessGuard);
        TimesheetsRequestContext context = Context();
        TenantReference tenant = context.Tenant.ShouldNotBeNull();
        PartyReference actor = context.Actor.ShouldNotBeNull();
        RecordTimeEntry record = RecordCommand(TimeEntryId(), ActivityId());

        TimeEntryCommandResult recordResult = await recordService.RecordAsync(
            context,
            record,
            null,
            FreshCatalog(ActiveCatalogItem(ActivityId())),
            TestContext.Current.CancellationToken);

        recordResult.WasDispatched.ShouldBeTrue();
        TimeEntryRecorded recorded = recordResult.DomainResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimeEntryRecorded>();

        TimeEntryState state = StateFrom(recorded);
        DateTimeOffset submittedAtUtc = new(2026, 6, 19, 8, 0, 0, TimeSpan.Zero);
        SubmitTimeEntriesForApproval submit = SubmitCommand(TimeEntryId());

        TimeEntrySubmissionCommandResult submitResult = await submissionService.SubmitAsync(
            context,
            submit,
            new Dictionary<TimeEntryId, TimeEntryState?> { [TimeEntryId()] = state },
            FreshCatalog(ActiveCatalogItem(ActivityId())),
            submittedAtUtc,
            TestContext.Current.CancellationToken);

        submitResult.HasAcceptedEvents.ShouldBeTrue();
        submitResult.HasBlockedEntries.ShouldBeFalse();
        TimeEntrySubmissionEntryResult submittedEntry = submitResult.Entries.ShouldHaveSingleItem();
        submittedEntry.WasDispatched.ShouldBeTrue();
        TimeEntrySubmitted submitted = submittedEntry.DomainResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimeEntrySubmitted>();
        submitted.TimeEntryId.ShouldBe(TimeEntryId());
        submitted.Submitter.ShouldBe(actor);
        submitted.Tenant.ShouldBe(tenant);
        submitted.SubmittedAtUtc.ShouldBe(submittedAtUtc);
        submitted.TimeEntrySubmissionId.ShouldBe(submit.TimeEntrySubmissionId);
        submitted.ApprovalState.ShouldBe(TimeEntryApprovalState.Submitted);

        TimeEntryEvidenceReadModel projected = new TimeEntryEvidenceProjection().Project(
            tenant.TenantId,
            TimeEntryId(),
            [
                new("message-2", 2, submitted),
                new("message-1", 1, recorded),
                new("message-2", 2, submitted)
            ],
            new(tenant.TenantId, TimeEntryEvidenceProjection.ProjectionName, 2, ProjectionFreshness.Fresh))
            .ShouldNotBeNull();

        TimeEntryEvidenceQueryService queryService = new(
            accessGuard,
            new FixedProjectionReader(projected),
            new FixedDisplayHydrator());

        TimeEntryEvidenceQueryResult queryResult = await queryService.ReadAsync(
            context,
            TimeEntryId(),
            TestContext.Current.CancellationToken);

        queryResult.WasDisclosed.ShouldBeTrue();
        TimeEntryEvidenceReadModel evidence = queryResult.Evidence.ShouldNotBeNull();
        evidence.ApprovalState.ShouldBe(TimeEntryApprovalState.Submitted);
        evidence.DurationMinutes.ShouldBe(60);
        evidence.BillableState.ShouldBe(BillableState.Billable);
        evidence.CorrectionState.ShouldBe(TimeEntryCorrectionState.None);
        evidence.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
        evidence.SourceAuthority.ShouldBe(TimeEntryEvidenceSourceAuthority.TimesheetsDomainEvents);
        evidence.EventLineage.Select(static item => item.EventName).ShouldBe(
        [
            nameof(TimeEntryRecorded),
            nameof(TimeEntrySubmitted)
        ]);
        evidence.DisplayHydration.Contributor.Label.ShouldBe("Contributor");
        evidence.DisplayHydration.Target.Label.ShouldBe("Project");
        accessGuard.Requests.Select(static request => request.Operation).ShouldBe(
        [
            TimesheetsOperation.Command,
            TimesheetsOperation.Command,
            TimesheetsOperation.ProjectionRead,
            TimesheetsOperation.ProjectionRead
        ]);

        string json = JsonSerializer.Serialize(evidence, JsonOptions);

        json.ShouldContain("\"approvalState\":\"Submitted\"");
        json.ShouldContain("\"durationMinutes\":60");
        AssertJsonOmitsCallerAuthority(json);
    }

    [Fact]
    public async Task Submission_workflow_reports_partial_batch_when_activity_type_becomes_inactive()
    {
        AllowAllAccessGuard accessGuard = new();
        TimeEntrySubmissionCommandService submissionService = new(accessGuard);
        TimeEntryId validEntryId = TimeEntryId();
        TimeEntryId blockedEntryId = new("time-entry-blocked");
        ActivityTypeId blockedActivityId = new("activity-type-inactive");
        SubmitTimeEntriesForApproval submit = SubmitCommand(validEntryId, blockedEntryId);

        TimeEntrySubmissionCommandResult submitResult = await submissionService.SubmitAsync(
            Context(),
            submit,
            new Dictionary<TimeEntryId, TimeEntryState?>
            {
                [validEntryId] = StateFrom(Recorded(validEntryId, ActivityId())),
                [blockedEntryId] = StateFrom(Recorded(blockedEntryId, blockedActivityId))
            },
            FreshCatalog(
                ActiveCatalogItem(ActivityId()),
                InactiveCatalogItem(blockedActivityId)),
            new DateTimeOffset(2026, 6, 19, 8, 30, 0, TimeSpan.Zero),
            TestContext.Current.CancellationToken);

        submitResult.IsPartial.ShouldBeTrue();
        submitResult.HasAcceptedEvents.ShouldBeTrue();
        submitResult.HasBlockedEntries.ShouldBeTrue();

        TimeEntrySubmissionEntryResult accepted = submitResult.Entries
            .Single(entry => entry.TimeEntryId == validEntryId);
        accepted.WasDispatched.ShouldBeTrue();
        accepted.DomainResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimeEntrySubmitted>()
            .ApprovalState.ShouldBe(TimeEntryApprovalState.Submitted);

        TimeEntrySubmissionEntryResult blocked = submitResult.Entries
            .Single(entry => entry.TimeEntryId == blockedEntryId);
        blocked.WasDispatched.ShouldBeFalse();
        TimesheetsRejection rejection = blocked.DomainResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimesheetsRejection>();
        rejection.Code.ShouldBe(TimesheetsRejectionCode.ActivityTypeInactive);
        TimesheetsFieldError error = rejection.FieldErrors.ShouldHaveSingleItem();
        error.Field.ShouldBe("entries[time-entry-blocked].activityTypeId");
        error.Code.ShouldBe("unavailable");
    }

    private static RecordTimeEntry RecordCommand(
        TimeEntryId timeEntryId,
        ActivityTypeId activityTypeId)
        => new(
            timeEntryId,
            TimeEntryTargetReference.ForProject(Project()),
            Contributor(),
            activityTypeId,
            new DateOnly(2026, 6, 19),
            60,
            BillableState.Billable,
            ContributorCategory.Employee,
            AiEffortMetrics.Unavailable);

    private static SubmitTimeEntriesForApproval SubmitCommand(params TimeEntryId[] timeEntryIds)
        => new(
            new TimeEntrySubmissionId("submission-1"),
            timeEntryIds,
            TimeEntrySubmissionScope.SelectedEntries);

    private static TimeEntryRecorded Recorded(
        TimeEntryId timeEntryId,
        ActivityTypeId activityTypeId)
        => new(
            timeEntryId,
            TimeEntryTargetReference.ForProject(Project()),
            Contributor(),
            activityTypeId,
            ActivityTypeScope.Tenant,
            new DateOnly(2026, 6, 19),
            60,
            BillableState.Billable,
            TimeEntryApprovalState.Draft,
            ContributorCategory.Employee,
            AiEffortMetrics.Unavailable);

    private static TimeEntryState StateFrom(TimeEntryRecorded recorded)
    {
        TimeEntryState state = new();
        state.Apply(recorded);
        return state;
    }

    private static ActivityTypeCatalogReadModel FreshCatalog(params ActivityTypeCatalogItem[] items)
        => new(items, ProjectionFreshnessMetadata.Fresh);

    private static ActivityTypeCatalogItem ActiveCatalogItem(ActivityTypeId activityTypeId)
        => new(
            activityTypeId,
            ActivityTypeScope.Tenant,
            null,
            "Delivery",
            true,
            BillableState.Billable);

    private static ActivityTypeCatalogItem InactiveCatalogItem(ActivityTypeId activityTypeId)
        => new(
            activityTypeId,
            ActivityTypeScope.Tenant,
            null,
            "Inactive",
            false,
            BillableState.Billable);

    private static TimesheetsRequestContext Context()
        => new(
            new TenantReference("tenant-1"),
            new PartyReference("operator-1"),
            "correlation-1");

    private static ProjectReference Project() => new("project-1");

    private static PartyReference Contributor() => new("party-1");

    private static ActivityTypeId ActivityId() => new("activity-type-1");

    private static TimeEntryId TimeEntryId() => new("time-entry-1");

    private static void AssertJsonOmitsCallerAuthority(string json)
    {
        string normalizedJson = json.ToLowerInvariant();
        string[] forbiddenPropertyNames =
        [
            "tenantId",
            "userId",
            "correlationId",
            "messageId",
            "causationId",
            "authorization",
            "claimsPrincipal",
            "jwt",
            "token",
            "stream",
            "sequence"
        ];

        foreach (string forbiddenPropertyName in forbiddenPropertyNames)
        {
            normalizedJson.Contains(
                $"\"{forbiddenPropertyName.ToLowerInvariant()}\"",
                StringComparison.Ordinal).ShouldBeFalse(forbiddenPropertyName);
        }
    }

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
                TimeEntryHydratedDisplayLabel.Fresh("Contributor"),
                TimeEntryHydratedDisplayLabel.Fresh("Project"),
                TimeEntryHydratedDisplayLabel.Fresh("Delivery")));
    }
}
