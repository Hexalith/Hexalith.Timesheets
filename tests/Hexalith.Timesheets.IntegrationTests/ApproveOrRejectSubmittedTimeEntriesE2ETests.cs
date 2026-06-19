using System.Text.Json;

using Hexalith.Timesheets.Contracts.Commands.TimeEntries;
using Hexalith.Timesheets.Contracts.Events.Rejections;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Policies;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections;
using Hexalith.Timesheets.Projections.TimeEntries;
using Hexalith.Timesheets.Server.ApprovalAuthority;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.TimeEntries;

using Shouldly;

namespace Hexalith.Timesheets.IntegrationTests;

public sealed class ApproveOrRejectSubmittedTimeEntriesE2ETests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Submitted_time_entry_approval_workflow_records_projects_and_discloses_approved_evidence()
    {
        AllowAllAccessGuard accessGuard = new();
        FixedAuthorityProvider authorityProvider = AllowingAuthorityProvider();
        TimeEntryApprovalCommandService approvalService = ApprovalService(accessGuard, authorityProvider);
        TimeEntryState state = SubmittedState();
        ApproveTimeEntry command = new(TimeEntryId(), new TimeEntryApprovalDecisionId("approval-decision-1"));

        TimeEntryApprovalCommandResult approvalResult = await approvalService.ApproveAsync(
            Context(),
            command,
            state,
            DecidedAtUtc(),
            TestContext.Current.CancellationToken);

        approvalResult.WasDispatched.ShouldBeTrue();
        approvalResult.HasAcceptedEvents.ShouldBeTrue();
        TimeEntryApproved approved = approvalResult.DomainResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimeEntryApproved>();
        approved.TimeEntryId.ShouldBe(TimeEntryId());
        approved.Approver.ShouldBe(Approver());
        approved.Tenant.ShouldBe(Tenant());
        approved.DecidedAtUtc.ShouldBe(DecidedAtUtc());
        approved.TimeEntryApprovalDecisionId.ShouldBe(command.TimeEntryApprovalDecisionId);
        approved.ApprovalState.ShouldBe(TimeEntryApprovalState.Approved);
        approved.ApprovalScope.ShouldBe(TimeEntryApprovalScope.IndividualEntry);
        approved.AuthoritySource.Action.ShouldBe(ApprovalAuthorityAction.EntryApproval);
        approved.AuthoritySource.Source.ShouldBe(ApprovalAuthoritySource.ProjectApprover);

        ApprovalAuthorityResolutionRequest authorityRequest = authorityProvider.Requests.ShouldHaveSingleItem();
        authorityRequest.Action.ShouldBe(ApprovalAuthorityAction.EntryApproval);
        authorityRequest.Contributor.ShouldBe(Contributor());
        authorityRequest.AuthorizationRequest.Contributor.ShouldBe(Contributor());
        authorityRequest.AuthorizationRequest.Project.ShouldBe(Project());

        TimeEntryEvidenceReadModel projected = Project(approved);
        TimeEntryEvidenceReadModel evidence = await DiscloseEvidenceAsync(accessGuard, projected);

        evidence.ApprovalState.ShouldBe(TimeEntryApprovalState.Approved);
        evidence.ApprovalDecision.ShouldNotBeNull().ShouldSatisfyAllConditions(
            decision => decision.TimeEntryId.ShouldBe(TimeEntryId()),
            decision => decision.TimeEntryApprovalDecisionId.ShouldBe(command.TimeEntryApprovalDecisionId),
            decision => decision.Approver.ShouldBe(Approver()),
            decision => decision.Tenant.ShouldBe(Tenant()),
            decision => decision.DecidedAtUtc.ShouldBe(DecidedAtUtc()),
            decision => decision.ApprovalState.ShouldBe(TimeEntryApprovalState.Approved),
            decision => decision.ApprovalScope.ShouldBe(TimeEntryApprovalScope.IndividualEntry),
            decision => decision.AuthoritySource.Source.ShouldBe(ApprovalAuthoritySource.ProjectApprover),
            decision => decision.Reason.ShouldBeNull());
        evidence.EventLineage.Select(static item => item.EventName).ShouldBe(
        [
            nameof(TimeEntryRecorded),
            nameof(TimeEntrySubmitted),
            nameof(TimeEntryApproved)
        ]);
        evidence.DisplayHydration.Contributor.Label.ShouldBe("Contributor");
        evidence.DisplayHydration.Target.Label.ShouldBe("Project");
        evidence.DisplayHydration.ActivityType.Label.ShouldBe("Delivery");
        accessGuard.Requests.Select(static request => request.Operation).ShouldBe(
        [
            TimesheetsOperation.Command,
            TimesheetsOperation.ProjectionRead,
            TimesheetsOperation.ProjectionRead
        ]);

        string json = JsonSerializer.Serialize(evidence, JsonOptions);

        json.ShouldContain("\"approvalState\":\"Approved\"");
        json.ShouldContain("\"lockState\":\"LockedFromDirectEdit\"");
        json.ShouldContain("\"approvalScope\":\"IndividualEntry\"");
        json.ShouldContain("\"source\":\"ProjectApprover\"");
        AssertJsonOmitsCallerAndEnvelopeAuthority(json);
    }

    [Fact]
    public async Task Approved_time_entry_direct_mutation_workflow_rejects_and_discloses_lock_evidence()
    {
        AllowAllAccessGuard accessGuard = new();
        FixedAuthorityProvider authorityProvider = AllowingAuthorityProvider();
        TimeEntryApprovalCommandService approvalService = ApprovalService(accessGuard, authorityProvider);
        TimeEntryCommandService recordService = new(accessGuard);
        TimeEntryState state = SubmittedState();
        ApproveTimeEntry approve = new(TimeEntryId(), new TimeEntryApprovalDecisionId("approval-decision-1"));

        TimeEntryApprovalCommandResult approvalResult = await approvalService.ApproveAsync(
            Context(),
            approve,
            state,
            DecidedAtUtc(),
            TestContext.Current.CancellationToken);

        TimeEntryApproved approved = approvalResult.DomainResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimeEntryApproved>();
        state.Apply(approved);

        TimeEntryCommandResult mutationResult = await recordService.RecordAsync(
            Context(),
            DirectMutationCommand(),
            state,
            FreshCatalog(),
            TestContext.Current.CancellationToken);

        mutationResult.WasDispatched.ShouldBeTrue();
        TimesheetsRejection rejection = mutationResult.DomainResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimesheetsRejection>();
        rejection.Code.ShouldBe(TimesheetsRejectionCode.TimeEntryLocked);
        rejection.FieldErrors.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            error => error.Field.ShouldBe("entries[time-entry-1].lockState"),
            error => error.Code.ShouldBe("locked-from-direct-edit"));
        state.ApprovalState.ShouldBe(TimeEntryApprovalState.Approved);
        state.LockState.ShouldBe(TimeEntryLockState.LockedFromDirectEdit);

        TimeEntryEvidenceReadModel evidence = await DiscloseEvidenceAsync(accessGuard, Project(approved));

        evidence.ApprovalState.ShouldBe(TimeEntryApprovalState.Approved);
        evidence.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
        evidence.LockEvidence.ShouldSatisfyAllConditions(
            lockEvidence => lockEvidence.LockState.ShouldBe(TimeEntryLockState.LockedFromDirectEdit),
            lockEvidence => lockEvidence.SourceApprovalDecisionId.ShouldBe(approve.TimeEntryApprovalDecisionId),
            lockEvidence => lockEvidence.SourceApprovalScope.ShouldBe(TimeEntryApprovalScope.IndividualEntry),
            lockEvidence => lockEvidence.LockedBy.ShouldBe(Approver()),
            lockEvidence => lockEvidence.LockedAtUtc.ShouldBe(DecidedAtUtc()),
            lockEvidence => lockEvidence.Explanation.ShouldBe("Approved entries are locked from direct edits."));
        evidence.EventLineage.Select(static item => item.EventName).ShouldBe(
        [
            nameof(TimeEntryRecorded),
            nameof(TimeEntrySubmitted),
            nameof(TimeEntryApproved)
        ]);
        accessGuard.Requests.Select(static request => request.Operation).ShouldBe(
        [
            TimesheetsOperation.Command,
            TimesheetsOperation.Command,
            TimesheetsOperation.ProjectionRead,
            TimesheetsOperation.ProjectionRead
        ]);

        string json = JsonSerializer.Serialize(new { rejection, evidence }, JsonOptions);

        json.ShouldContain("\"code\":\"TimeEntryLocked\"");
        json.ShouldContain("\"lockState\":\"LockedFromDirectEdit\"");
        json.ShouldContain("Approved entries are locked from direct edits.");
        AssertJsonOmitsCallerAndEnvelopeAuthority(json);
    }

    [Fact]
    public async Task Submitted_time_entry_rejection_workflow_requires_reason_and_discloses_rejected_evidence()
    {
        AllowAllAccessGuard accessGuard = new();
        FixedAuthorityProvider authorityProvider = AllowingAuthorityProvider();
        TimeEntryApprovalCommandService approvalService = ApprovalService(accessGuard, authorityProvider);
        TimeEntryRejectionReason reason = new("Needs customer PO evidence.");
        RejectTimeEntry command = new(TimeEntryId(), new TimeEntryApprovalDecisionId("rejection-decision-1"), reason);

        TimeEntryApprovalCommandResult rejectionResult = await approvalService.RejectAsync(
            Context(),
            command,
            SubmittedState(),
            DecidedAtUtc(),
            TestContext.Current.CancellationToken);

        rejectionResult.WasDispatched.ShouldBeTrue();
        rejectionResult.HasAcceptedEvents.ShouldBeTrue();
        TimeEntryRejected rejected = rejectionResult.DomainResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimeEntryRejected>();
        rejected.TimeEntryId.ShouldBe(TimeEntryId());
        rejected.Approver.ShouldBe(Approver());
        rejected.Tenant.ShouldBe(Tenant());
        rejected.DecidedAtUtc.ShouldBe(DecidedAtUtc());
        rejected.TimeEntryApprovalDecisionId.ShouldBe(command.TimeEntryApprovalDecisionId);
        rejected.ApprovalState.ShouldBe(TimeEntryApprovalState.Rejected);
        rejected.ApprovalScope.ShouldBe(TimeEntryApprovalScope.IndividualEntry);
        rejected.AuthoritySource.Action.ShouldBe(ApprovalAuthorityAction.EntryRejection);
        rejected.Reason.ShouldBe(reason);

        ApprovalAuthorityResolutionRequest authorityRequest = authorityProvider.Requests.ShouldHaveSingleItem();
        authorityRequest.Action.ShouldBe(ApprovalAuthorityAction.EntryRejection);
        authorityRequest.Contributor.ShouldBe(Contributor());
        authorityRequest.AuthorizationRequest.Contributor.ShouldBe(Contributor());

        TimeEntryEvidenceReadModel evidence = await DiscloseEvidenceAsync(accessGuard, Project(rejected));

        evidence.ApprovalState.ShouldBe(TimeEntryApprovalState.Rejected);
        evidence.ApprovalDecision.ShouldNotBeNull().ShouldSatisfyAllConditions(
            decision => decision.TimeEntryApprovalDecisionId.ShouldBe(command.TimeEntryApprovalDecisionId),
            decision => decision.Approver.ShouldBe(Approver()),
            decision => decision.Tenant.ShouldBe(Tenant()),
            decision => decision.DecidedAtUtc.ShouldBe(DecidedAtUtc()),
            decision => decision.ApprovalState.ShouldBe(TimeEntryApprovalState.Rejected),
            decision => decision.AuthoritySource.Action.ShouldBe(ApprovalAuthorityAction.EntryRejection),
            decision => decision.Reason.ShouldBe(reason));
        evidence.EventLineage.Select(static item => item.EventName).ShouldBe(
        [
            nameof(TimeEntryRecorded),
            nameof(TimeEntrySubmitted),
            nameof(TimeEntryRejected)
        ]);

        string json = JsonSerializer.Serialize(evidence, JsonOptions);

        json.ShouldContain("\"approvalState\":\"Rejected\"");
        json.ShouldContain("Needs customer PO evidence.");
        AssertJsonOmitsCallerAndEnvelopeAuthority(json);
    }

    [Fact]
    public async Task Approval_workflow_fails_closed_when_authority_cannot_be_resolved()
    {
        AllowAllAccessGuard accessGuard = new();
        FixedAuthorityProvider authorityProvider = new(ApprovalAuthoritySource.ProjectApprover, ApprovalAuthoritySourceResult.Denied(
            ApprovalAuthoritySource.ProjectApprover,
            ApprovalAuthorityDecisionState.Ambiguous,
            TimesheetsDenialCategory.AmbiguousAuthority,
            "tenant-1 project-1 role Owner upstream detail",
            ProjectionFreshnessMetadata.Stale()));
        TimeEntryApprovalCommandService approvalService = ApprovalService(accessGuard, authorityProvider);

        TimeEntryApprovalCommandResult approvalResult = await approvalService.ApproveAsync(
            Context(),
            new ApproveTimeEntry(TimeEntryId(), new TimeEntryApprovalDecisionId("approval-decision-1")),
            SubmittedState(),
            DecidedAtUtc(),
            TestContext.Current.CancellationToken);

        approvalResult.WasDispatched.ShouldBeFalse();
        approvalResult.HasAcceptedEvents.ShouldBeFalse();
        approvalResult.HasAuthorityDenial.ShouldBeTrue();
        approvalResult.DomainResult.ShouldBeNull();
        approvalResult.AuthorityResolution.ShouldNotBeNull().ShouldSatisfyAllConditions(
            authority => authority.IsAllowed.ShouldBeFalse(),
            authority => authority.DenialCategory.ShouldBe(TimesheetsDenialCategory.AmbiguousAuthority),
            authority => authority.Reason.ShouldBe("Authority cannot be resolved."),
            authority => authority.Reason.ShouldNotContain("project", Case.Insensitive),
            authority => authority.Reason.ShouldNotContain("role", Case.Insensitive),
            authority => authority.SourceAttribution.DecisionState.ShouldBe(ApprovalAuthorityDecisionState.Ambiguous),
            authority => authority.SourceAttribution.Freshness.State.ShouldBe(ProjectionFreshnessState.Stale));
        authorityProvider.Requests.ShouldHaveSingleItem().Action.ShouldBe(ApprovalAuthorityAction.EntryApproval);
        accessGuard.Requests.Select(static request => request.Operation).ShouldBe([TimesheetsOperation.Command]);

        string json = JsonSerializer.Serialize(approvalResult.AuthorityResolution, JsonOptions);

        json.ShouldContain("Authority cannot be resolved.");
        json.ShouldNotContain("tenant-1");
        json.ShouldNotContain("project-1");
        json.ShouldNotContain("Owner");
        AssertJsonOmitsCallerAndEnvelopeAuthority(json);
    }

    private static TimeEntryApprovalCommandService ApprovalService(
        AllowAllAccessGuard accessGuard,
        FixedAuthorityProvider authorityProvider)
        => new(
            accessGuard,
            new TimesheetsApprovalAuthorityResolver(
                new TimesheetsApprovalAuthorityPolicyOptions
                {
                    PolicyVersion = "v2"
                },
                [authorityProvider]));

    private static FixedAuthorityProvider AllowingAuthorityProvider()
        => new(
            ApprovalAuthoritySource.ProjectApprover,
            ApprovalAuthoritySourceResult.Allowed(
                ApprovalAuthoritySource.ProjectApprover,
                ProjectionFreshnessMetadata.Fresh));

    private static async ValueTask<TimeEntryEvidenceReadModel> DiscloseEvidenceAsync(
        AllowAllAccessGuard accessGuard,
        TimeEntryEvidenceReadModel projected)
    {
        TimeEntryEvidenceQueryService queryService = new(
            accessGuard,
            new FixedProjectionReader(projected),
            new FixedDisplayHydrator());

        TimeEntryEvidenceQueryResult queryResult = await queryService.ReadAsync(
            Context(),
            TimeEntryId(),
            TestContext.Current.CancellationToken);

        queryResult.WasDisclosed.ShouldBeTrue();
        return queryResult.Evidence.ShouldNotBeNull();
    }

    private static TimeEntryEvidenceReadModel Project(object approvalDecision)
    {
        return new TimeEntryEvidenceProjection().Project(
            Tenant().TenantId,
            TimeEntryId(),
            [
                new("message-3", 3, approvalDecision),
                new("message-2", 2, Submitted()),
                new("message-1", 1, Recorded()),
                new("message-3", 3, approvalDecision)
            ],
            new(Tenant().TenantId, TimeEntryEvidenceProjection.ProjectionName, 3, ProjectionFreshness.Fresh))
            .ShouldNotBeNull();
    }

    private static TimeEntryState SubmittedState()
    {
        TimeEntryState state = new();
        state.Apply(Recorded());
        state.Apply(Submitted());
        return state;
    }

    private static RecordTimeEntry DirectMutationCommand()
        => new(
            TimeEntryId(),
            TimeEntryTargetReference.ForProject(Project()),
            Contributor(),
            ActivityId(),
            new DateOnly(2026, 6, 20),
            90,
            BillableState.NonBillable,
            ContributorCategory.Employee,
            AiEffortMetrics.Unavailable)
        {
            Comment = new("Attempted direct mutation after approval.", TimeEntryCommentPolicy.SensitiveDefault)
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

    private static TimeEntryRecorded Recorded()
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
            ContributorCategory.Employee,
            AiEffortMetrics.Unavailable);

    private static TimeEntrySubmitted Submitted()
        => new(
            TimeEntryId(),
            Contributor(),
            Tenant(),
            new DateTimeOffset(2026, 6, 19, 8, 0, 0, TimeSpan.Zero),
            new TimeEntrySubmissionId("submission-1"),
            TimeEntrySubmissionScope.SelectedEntries,
            TimeEntryApprovalState.Submitted);

    private static TimesheetsRequestContext Context()
        => new(Tenant(), Approver(), "correlation-1");

    private static TenantReference Tenant() => new("tenant-1");

    private static PartyReference Approver() => new("party-approver");

    private static PartyReference Contributor() => new("party-contributor");

    private static ProjectReference Project() => new("project-1");

    private static ActivityTypeId ActivityId() => new("activity-type-1");

    private static TimeEntryId TimeEntryId() => new("time-entry-1");

    private static DateTimeOffset DecidedAtUtc() => new(2026, 6, 19, 10, 0, 0, TimeSpan.Zero);

    private static void AssertJsonOmitsCallerAndEnvelopeAuthority(string json)
    {
        string normalizedJson = json.ToLowerInvariant();
        string[] forbiddenPropertyNames =
        [
            "userId",
            "correlationId",
            "messageId",
            "causationId",
            "authorization",
            "claimsPrincipal",
            "jwt",
            "token",
            "roles",
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

    private sealed class FixedAuthorityProvider(
        ApprovalAuthoritySource source,
        ApprovalAuthoritySourceResult result)
        : IApprovalAuthoritySourceProvider
    {
        public List<ApprovalAuthorityResolutionRequest> Requests { get; } = [];

        public ApprovalAuthoritySource Source { get; } = source;

        public int Precedence => TimesheetsApprovalAuthorityPolicyOptions.DefaultPrecedence(Source);

        public ValueTask<ApprovalAuthoritySourceResult> EvaluateAsync(
            ApprovalAuthorityResolutionRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return ValueTask.FromResult(result);
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
                request.UiAction ?? TimesheetsUiAction.Approval));
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
