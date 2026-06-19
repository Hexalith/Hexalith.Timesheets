using System.Text.Json;

using Hexalith.Timesheets.Contracts.Commands.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections;
using Hexalith.Timesheets.Projections.MagicLinks;
using Hexalith.Timesheets.Projections.TimeEntries;
using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.MagicLinks;
using Hexalith.Timesheets.Server.TimeEntries;

using Shouldly;

using CapabilityState = Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState;
using ServerCapabilityState = Hexalith.Timesheets.Server.MagicLinks.MagicLinkCapabilityState;

namespace Hexalith.Timesheets.IntegrationTests;

public sealed class MagicLinkConfirmationCapabilityWorkflowE2ETests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Magic_link_issue_revoke_and_operator_projection_workflow_discloses_state_without_token_material()
    {
        RecordingAccessGuard accessGuard = new(TimesheetsAuthorizationDecision.Allowed());
        DeterministicTokenGenerator tokenGenerator = new();
        MagicLinkConfirmationCapabilityCommandService service = CreateService(accessGuard, tokenGenerator);

        MagicLinkCapabilityCommandResult issueResult = await service.IssueAsync(
            Context(),
            IssueCommand(),
            null,
            FreshCatalog(),
            IssuedAtUtc(),
            TestContext.Current.CancellationToken);

        issueResult.WasDispatched.ShouldBeTrue();
        issueResult.IssueResponse.ShouldNotBeNull().OneTimeToken.ShouldBe("opaque-once");
        MagicLinkConfirmationCapabilityIssued issued = issueResult.DomainResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<MagicLinkConfirmationCapabilityIssued>();

        ServerCapabilityState state = new();
        state.Apply(issued);

        MagicLinkCapabilityCommandResult revokeResult = await service.RevokeAsync(
            Context(),
            RevokeCommand(),
            state,
            RevokedAtUtc(),
            TestContext.Current.CancellationToken);

        revokeResult.WasDispatched.ShouldBeTrue();
        MagicLinkConfirmationCapabilityRevoked revoked = revokeResult.DomainResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<MagicLinkConfirmationCapabilityRevoked>();

        MagicLinkConfirmationCapabilityReadModel readModel = new MagicLinkConfirmationCapabilityProjection().Project(
            CapabilityId(),
            [
                new("message-2", 2, revoked),
                new("message-1", 1, issued),
                new("message-2", 2, revoked)
            ],
            new("tenant-1", MagicLinkConfirmationCapabilityProjection.ProjectionName, 2, ProjectionFreshness.Fresh),
            ObservedAtUtc())
            .ShouldNotBeNull();

        readModel.State.ShouldBe(CapabilityState.Revoked);
        readModel.StateBadgeText.ShouldBe("Revoked");
        readModel.ExpiryBadgeText.ShouldBe("Expiring soon");
        readModel.Contributor.ShouldBe(Contributor());
        readModel.Target.ShouldBe(TimeEntryTargetReference.ForProject(Project()));
        readModel.Issuer.ShouldBe(Operator());
        readModel.RevokedBy.ShouldBe(Operator());
        readModel.IssueMetadata.ShouldBe(new MagicLinkAuditMetadata("timesheets", "issue-1"));
        readModel.RevocationMetadata.ShouldBe(new MagicLinkAuditMetadata("timesheets", "revoke-1"));
        tokenGenerator.GenerateCount.ShouldBe(1);
        accessGuard.Requests.Select(static request => request.Operation).ShouldBe(
        [
            TimesheetsOperation.MagicLinkIssuance,
            TimesheetsOperation.MagicLinkIssuance
        ]);

        string issuedJson = JsonSerializer.Serialize(issued, JsonOptions);
        string readModelJson = JsonSerializer.Serialize(readModel, JsonOptions);

        issuedJson.ShouldNotContain("opaque-once");
        issuedJson.ShouldNotContain("oneTimeToken");
        readModelJson.ShouldNotContain("opaque-once");
        readModelJson.ShouldNotContain("token", Case.Insensitive);
        readModelJson.ShouldNotContain("comment", Case.Insensitive);
        readModelJson.ShouldNotContain("command", Case.Insensitive);
    }

    [Fact]
    public async Task Magic_link_adjustment_workflow_updates_draft_values_projects_safe_evidence_and_rejects_replay()
    {
        RecordingAccessGuard accessGuard = new(TimesheetsAuthorizationDecision.Allowed());
        DeterministicTokenGenerator tokenGenerator = new();
        MagicLinkConfirmationCapabilityCommandService service = CreateService(accessGuard, tokenGenerator);
        TimeEntryRecorded recorded = RecordedExternalEvent();
        TimeEntryState timeEntryState = new();
        timeEntryState.Apply(recorded);

        MagicLinkCapabilityCommandResult issueResult = await service.IssueAsync(
            Context(),
            IssueCommand(MagicLinkAllowedAction.Adjust),
            null,
            FreshCatalog(),
            IssuedAtUtc(),
            TestContext.Current.CancellationToken);

        MagicLinkConfirmationCapabilityIssued issued = issueResult.DomainResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<MagicLinkConfirmationCapabilityIssued>();
        MagicLinkIssueResponse issueResponse = issueResult.IssueResponse.ShouldNotBeNull();
        ServerCapabilityState capabilityState = new();
        capabilityState.Apply(issued);

        MagicLinkAdjustmentDisplayResponse display = (await service.DescribeAdjustmentAsync(
            Context(),
            issueResponse.OneTimeToken,
            capabilityState,
            timeEntryState,
            FreshCatalog(),
            ConfirmedAtUtc(),
            TestContext.Current.CancellationToken)).ShouldNotBeNull();
        display.ProposedDate.ShouldBe(new DateOnly(2026, 6, 19));
        display.DurationMinutes.ShouldBe(60);
        display.DurationUnit.ShouldBe("minutes");
        display.ActivityTypeLabel.ShouldBe("Delivery");
        display.EditableFields.ShouldBe(["serviceDate", "durationMinutes", "activityTypeId", "billableState", "comment"]);
        display.ReadOnlyFields.ShouldBe(["target", "contributor", "tenant", "timeEntryId", "approvalState"]);
        string displayJson = JsonSerializer.Serialize(display, JsonOptions);
        displayJson.ShouldNotContain(issueResponse.OneTimeToken);
        displayJson.ShouldNotContain("token", Case.Insensitive);
        displayJson.ShouldNotContain("party", Case.Insensitive);

        MagicLinkConfirmationUseResult invalidAdjustment = await service.AdjustAsync(
            Context(),
            issueResponse.OneTimeToken,
            AdjustCommand() with { DurationMinutes = 0 },
            capabilityState,
            timeEntryState,
            FreshCatalog(),
            ConfirmedAtUtc(),
            TestContext.Current.CancellationToken);

        invalidAdjustment.WasDispatched.ShouldBeFalse();
        invalidAdjustment.CapabilityResult.ShouldBeNull();
        invalidAdjustment.AdjustmentResult.ShouldNotBeNull().DomainResult.ShouldNotBeNull().IsRejection.ShouldBeTrue();

        MagicLinkConfirmationUseResult adjustment = await service.AdjustAsync(
            Context(),
            issueResponse.OneTimeToken,
            AdjustCommand(),
            capabilityState,
            timeEntryState,
            FreshCatalog(),
            ConfirmedAtUtc(),
            TestContext.Current.CancellationToken);

        adjustment.WasDispatched.ShouldBeTrue();
        TimeEntryAdjustedThroughMagicLink adjusted = adjustment.AdjustmentResult.ShouldNotBeNull()
            .DomainResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimeEntryAdjustedThroughMagicLink>();
        adjusted.AdjustedValues.ServiceDate.ShouldBe(new DateOnly(2026, 6, 20));
        adjusted.AdjustedValues.DurationMinutes.ShouldBe(75);
        adjusted.AdjustedValues.BillableState.ShouldBe(BillableState.NonBillable);
        adjusted.AdjustedValues.Target.ShouldBe(TimeEntryTargetReference.ForProject(Project()));
        adjusted.AdjustedValues.Contributor.ShouldBe(Contributor());
        adjusted.PreviousValues.DurationMinutes.ShouldBe(60);
        adjusted.Source.ShouldBe(new ExternalContributionSource("magic-link", "capability-1"));

        MagicLinkConfirmationCapabilityUsed used = adjustment.CapabilityResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<MagicLinkConfirmationCapabilityUsed>();
        used.OutcomeCategory.ShouldBe("adjusted");
        used.Source.ShouldBe(new MagicLinkAuditMetadata("magic-link", "capability-1"));

        MagicLinkConfirmationCapabilityReadModel capabilityReadModel = new MagicLinkConfirmationCapabilityProjection().Project(
            CapabilityId(),
            [new("message-1", 1, issued), new("message-2", 2, used), new("message-2", 2, used)],
            new("tenant-1", MagicLinkConfirmationCapabilityProjection.ProjectionName, 2, ProjectionFreshness.Fresh),
            ObservedAtUtc())
            .ShouldNotBeNull();

        capabilityReadModel.State.ShouldBe(CapabilityState.Used);
        capabilityReadModel.UseOutcomeCategory.ShouldBe("adjusted");
        capabilityReadModel.UseMetadata.ShouldBe(new MagicLinkAuditMetadata("magic-link", "capability-1"));
        string capabilityJson = JsonSerializer.Serialize(capabilityReadModel, JsonOptions);
        capabilityJson.ShouldNotContain(issueResponse.OneTimeToken);
        capabilityJson.ShouldNotContain("token", Case.Insensitive);
        capabilityJson.ShouldNotContain("command", Case.Insensitive);

        TimeEntryEvidenceReadModel evidence = new TimeEntryEvidenceProjection().Project(
            "tenant-1",
            TimeEntryId(),
            [
                new("time-message-1", 1, recorded),
                new("time-message-2", 2, adjusted),
                new("time-message-2", 2, adjusted)
            ],
            new("tenant-1", TimeEntryEvidenceProjection.ProjectionName, 2, ProjectionFreshness.Fresh))
            .ShouldNotBeNull();

        evidence.ServiceDate.ShouldBe(new DateOnly(2026, 6, 20));
        evidence.DurationMinutes.ShouldBe(75);
        evidence.BillableState.ShouldBe(BillableState.NonBillable);
        evidence.ApprovalState.ShouldBe(TimeEntryApprovalState.Draft);
        evidence.ApprovalDecision.ShouldBeNull();
        evidence.ExternalAdjustment.ShouldNotBeNull();
        evidence.ExternalAdjustment.PreviousValues.DurationMinutes.ShouldBe(60);
        evidence.ExternalAdjustment.AdjustedValues.DurationMinutes.ShouldBe(75);
        evidence.ExternalAdjustment.Source.ShouldBe(new ExternalContributionSource("magic-link", "capability-1"));
        evidence.EventLineage.Select(static item => item.EventName)
            .ShouldBe([nameof(TimeEntryRecorded), nameof(TimeEntryAdjustedThroughMagicLink)]);
        string evidenceJson = JsonSerializer.Serialize(evidence, JsonOptions);
        evidenceJson.ShouldNotContain(issueResponse.OneTimeToken);
        evidenceJson.ShouldNotContain("token", Case.Insensitive);
        evidenceJson.ShouldNotContain("command", Case.Insensitive);

        capabilityState.Apply(used);
        timeEntryState.Apply(adjusted);
        MagicLinkConfirmationUseResult replay = await service.AdjustAsync(
            Context(),
            issueResponse.OneTimeToken,
            AdjustCommand(),
            capabilityState,
            timeEntryState,
            FreshCatalog(),
            ConfirmedAtUtc(),
            TestContext.Current.CancellationToken);

        replay.WasDispatched.ShouldBeFalse();
        replay.CapabilityResult.ShouldNotBeNull().IsRejection.ShouldBeTrue();
        replay.AdjustmentResult.ShouldBeNull();
    }

    [Fact]
    public async Task Magic_link_issue_with_unfresh_activity_catalog_fails_closed_without_token_or_projection()
    {
        RecordingAccessGuard accessGuard = new(TimesheetsAuthorizationDecision.Allowed());
        DeterministicTokenGenerator tokenGenerator = new();
        MagicLinkConfirmationCapabilityCommandService service = CreateService(accessGuard, tokenGenerator);

        MagicLinkCapabilityCommandResult result = await service.IssueAsync(
            Context(),
            IssueCommand(),
            null,
            new ActivityTypeCatalogReadModel([], ProjectionFreshnessMetadata.Stale()),
            IssuedAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeTrue();
        result.DomainResult.ShouldNotBeNull().IsRejection.ShouldBeTrue();
        result.IssueResponse.ShouldBeNull();
        tokenGenerator.GenerateCount.ShouldBe(0);
        new MagicLinkConfirmationCapabilityProjection().Project(
            CapabilityId(),
            [],
            new("tenant-1", MagicLinkConfirmationCapabilityProjection.ProjectionName, 0, ProjectionFreshness.Stale),
            ObservedAtUtc()).ShouldBeNull();
    }

    [Fact]
    public async Task Magic_link_confirmation_workflow_consumes_token_projects_used_state_and_rejects_replay()
    {
        RecordingAccessGuard accessGuard = new(TimesheetsAuthorizationDecision.Allowed());
        DeterministicTokenGenerator tokenGenerator = new();
        MagicLinkConfirmationCapabilityCommandService service = CreateService(accessGuard, tokenGenerator);

        MagicLinkCapabilityCommandResult issueResult = await service.IssueAsync(
            Context(),
            IssueCommand(),
            null,
            FreshCatalog(),
            IssuedAtUtc(),
            TestContext.Current.CancellationToken);

        MagicLinkConfirmationCapabilityIssued issued = issueResult.DomainResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<MagicLinkConfirmationCapabilityIssued>();
        MagicLinkIssueResponse issueResponse = issueResult.IssueResponse.ShouldNotBeNull();
        ServerCapabilityState state = new();
        state.Apply(issued);

        MagicLinkConfirmationDisplayResponse display = (await service.DescribeAsync(
            Context(),
            issueResponse.OneTimeToken,
            state,
            RecordedExternalState(),
            FreshCatalog(),
            ConfirmedAtUtc(),
            TestContext.Current.CancellationToken)).ShouldNotBeNull();
        display.ProposedDate.ShouldBe(new DateOnly(2026, 6, 19));
        display.DurationMinutes.ShouldBe(60);
        display.ActivityTypeLabel.ShouldBe("Delivery");
        display.TargetContext.ShouldBe("Project");
        JsonSerializer.Serialize(display, JsonOptions).ShouldNotContain(issueResponse.OneTimeToken);

        MagicLinkConfirmationUseResult confirmation = await service.ConfirmAsync(
            Context(),
            issueResponse.OneTimeToken,
            ConfirmCommand(),
            state,
            RecordedExternalState(),
            ConfirmedAtUtc(),
            TestContext.Current.CancellationToken);

        confirmation.WasDispatched.ShouldBeTrue();
        MagicLinkConfirmationCapabilityUsed used = confirmation.CapabilityResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<MagicLinkConfirmationCapabilityUsed>();
        TimeEntryContributorConfirmed contributorConfirmed = confirmation.TimeEntryResult.ShouldNotBeNull()
            .DomainResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimeEntryContributorConfirmed>();
        contributorConfirmed.Source.ShouldBe(new ExternalContributionSource("magic-link", "capability-1"));

        MagicLinkConfirmationCapabilityReadModel readModel = new MagicLinkConfirmationCapabilityProjection().Project(
            CapabilityId(),
            [new("message-1", 1, issued), new("message-2", 2, used), new("message-2", 2, used)],
            new("tenant-1", MagicLinkConfirmationCapabilityProjection.ProjectionName, 2, ProjectionFreshness.Fresh),
            ObservedAtUtc())
            .ShouldNotBeNull();

        readModel.State.ShouldBe(CapabilityState.Used);
        readModel.StateBadgeText.ShouldBe("Used");
        readModel.UsedAtUtc.ShouldBe(ConfirmedAtUtc());
        readModel.UseMetadata.ShouldBe(new MagicLinkAuditMetadata("magic-link", "capability-1"));
        string readModelJson = JsonSerializer.Serialize(readModel, JsonOptions);
        readModelJson.ShouldNotContain(issueResponse.OneTimeToken);
        readModelJson.ShouldNotContain("token", Case.Insensitive);

        state.Apply(used);
        MagicLinkConfirmationUseResult replay = await service.ConfirmAsync(
            Context(),
            issueResponse.OneTimeToken,
            ConfirmCommand(),
            state,
            RecordedExternalState(),
            ConfirmedAtUtc(),
            TestContext.Current.CancellationToken);

        replay.WasDispatched.ShouldBeFalse();
        replay.CapabilityResult.ShouldNotBeNull().IsRejection.ShouldBeTrue();
        replay.TimeEntryResult.ShouldBeNull();

        // A used link discloses nothing on the display surface either (AC4 no-disclosure).
        (await service.DescribeAsync(
            Context(),
            issueResponse.OneTimeToken,
            state,
            RecordedExternalState(),
            FreshCatalog(),
            ConfirmedAtUtc(),
            TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    private static IssueMagicLinkConfirmationCapability IssueCommand(
        MagicLinkAllowedAction allowedAction = MagicLinkAllowedAction.Confirm)
        => new(
            CapabilityId(),
            new MagicLinkConfirmationScope(
                Contributor(),
                TimeEntryTargetReference.ForProject(Project()),
                ActivityId(),
                TimeEntryId(),
                MagicLinkTargetKind.ProposedTimeEntry),
            allowedAction,
            ExpiresAtUtc(),
            new MagicLinkAuditMetadata("timesheets", "issue-1"));

    private static RevokeMagicLinkConfirmationCapability RevokeCommand()
        => new(CapabilityId(), new MagicLinkAuditMetadata("timesheets", "revoke-1"));

    private static ConfirmTimeThroughMagicLink ConfirmCommand()
        => new();

    private static AdjustTimeThroughMagicLink AdjustCommand()
        => new(
            new DateOnly(2026, 6, 20),
            75,
            ActivityId(),
            BillableState.NonBillable);

    private static TimeEntryState RecordedExternalState()
    {
        TimeEntryState state = new();
        state.Apply(RecordedExternalEvent());
        return state;
    }

    private static TimeEntryRecorded RecordedExternalEvent()
        => new(
            TimeEntryId(),
            TimeEntryTargetReference.ForProject(Project()),
            Contributor(),
            ActivityId(),
            ActivityTypeScope.Tenant,
            new DateOnly(2026, 6, 19),
            60,
            BillableState.Billable,
            TimeEntryApprovalState.Draft,
            ContributorCategory.ExternalContributor,
            null)
        {
            ExternalSource = new ExternalContributionSource("supplier-api", "request-1")
        };

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

    private static MagicLinkConfirmationCapabilityCommandService CreateService(
        ITimesheetsAccessGuard accessGuard,
        IMagicLinkTokenGenerator tokenGenerator)
    {
        TimeEntryCommandService recordService = new(accessGuard);
        TimeEntrySubmissionCommandService submissionService = new(accessGuard);
        ExternalContributionCommandService externalContributionService = new(
            recordService,
            submissionService,
            accessGuard,
            new ExternalContributionPolicyOptions());

        return new(accessGuard, tokenGenerator, externalContributionService);
    }

    private static TimesheetsRequestContext Context()
        => new(new TenantReference("tenant-1"), Operator(), "correlation-1");

    private static DateTimeOffset IssuedAtUtc() => new(2026, 6, 19, 12, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset RevokedAtUtc() => new(2026, 6, 19, 13, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset ConfirmedAtUtc() => new(2026, 6, 19, 13, 30, 0, TimeSpan.Zero);

    private static DateTimeOffset ObservedAtUtc() => new(2026, 6, 19, 14, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset ExpiresAtUtc() => new(2026, 6, 20, 12, 0, 0, TimeSpan.Zero);

    private static MagicLinkCapabilityId CapabilityId() => new("capability-1");

    private static ProjectReference Project() => new("project-1");

    private static PartyReference Contributor() => new("party-1");

    private static PartyReference Operator() => new("operator-1");

    private static ActivityTypeId ActivityId() => new("activity-type-1");

    private static TimeEntryId TimeEntryId() => new("time-entry-1");

    private sealed class RecordingAccessGuard : ITimesheetsAccessGuard
    {
        private readonly TimesheetsAuthorizationDecision _decision;

        public RecordingAccessGuard(TimesheetsAuthorizationDecision decision)
        {
            ArgumentNullException.ThrowIfNull(decision);
            _decision = decision;
        }

        public List<TimesheetsAuthorizationRequest> Requests { get; } = [];

        public ValueTask<TimesheetsAuthorizationDecision> AuthorizeAsync(
            TimesheetsAuthorizationRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);

            return ValueTask.FromResult(_decision);
        }

        public async ValueTask<TimesheetsAuthorizationDecision> ExecuteIfAuthorizedAsync(
            TimesheetsAuthorizationRequest request,
            Func<CancellationToken, ValueTask> trustedWork,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);

            if (_decision.IsAuthorized)
            {
                await trustedWork(cancellationToken).ConfigureAwait(false);
            }

            return _decision;
        }

        public ValueTask<TimesheetsUiActionPolicyOutcome> EvaluateUiActionAsync(
            TimesheetsAuthorizationRequest request,
            TimesheetsUiActionVisibility deniedVisibility,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);

            return ValueTask.FromResult(TimesheetsUiActionPolicyOutcome.FromDecision(
                request.UiAction ?? TimesheetsUiAction.Capture,
                _decision,
                deniedVisibility));
        }
    }

    private sealed class DeterministicTokenGenerator : IMagicLinkTokenGenerator
    {
        public int GenerateCount { get; private set; }

        public MagicLinkTokenMaterial Generate()
        {
            GenerateCount++;
            return new("opaque-once", new MagicLinkTokenHash("hash-only"));
        }

        public MagicLinkTokenHash DeriveHash(string oneTimeToken)
            => oneTimeToken == "opaque-once"
                ? new MagicLinkTokenHash("hash-only")
                : new MagicLinkTokenHash("different-hash");
    }
}
