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

public sealed class CorrectRejectedTimeEntriesForResubmissionE2ETests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Rejected_time_entry_correction_workflow_preserves_rejection_lineage_and_resubmits_corrected_evidence()
    {
        ConfigurableAccessGuard accessGuard = new();
        FixedAuthorityProvider authorityProvider = AllowingAuthorityProvider();
        TimeEntryCommandService recordService = new(accessGuard);
        TimeEntrySubmissionCommandService submissionService = new(accessGuard);
        TimeEntryApprovalCommandService approvalService = ApprovalService(accessGuard, authorityProvider);
        TimeEntryCorrectionCommandService correctionService = CorrectionService(accessGuard, authorityProvider);
        ActivityTypeCatalogReadModel catalog = FreshCatalog(ActiveCatalogItem(ActivityId()));
        RecordTimeEntry record = RecordCommand();

        TimeEntryCommandResult recordResult = await recordService.RecordAsync(
            ContributorContext(),
            record,
            null,
            catalog,
            TestContext.Current.CancellationToken);

        TimeEntryRecorded recorded = recordResult.DomainResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimeEntryRecorded>();
        TimeEntryState state = StateFrom(recorded);

        TimeEntrySubmitted firstSubmission = await SubmitOneAsync(
            submissionService,
            ContributorContext(),
            state,
            new TimeEntrySubmissionId("submission-1"),
            new DateTimeOffset(2026, 6, 19, 8, 0, 0, TimeSpan.Zero),
            catalog);
        state.Apply(firstSubmission);

        TimeEntryRejectionReason rejectionReason = new("Needs customer PO evidence.");
        TimeEntryApprovalDecisionId rejectionDecisionId = new("rejection-decision-1");
        TimeEntryApprovalCommandResult rejectionResult = await approvalService.RejectAsync(
            ApproverContext(),
            new RejectTimeEntry(TimeEntryId(), rejectionDecisionId, rejectionReason),
            state,
            new DateTimeOffset(2026, 6, 19, 10, 0, 0, TimeSpan.Zero),
            TestContext.Current.CancellationToken);

        TimeEntryRejected rejected = rejectionResult.DomainResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimeEntryRejected>();
        state.Apply(rejected);

        CorrectRejectedTimeEntry correction = CorrectionCommand();
        TimeEntryCorrectionCommandResult correctionResult = await correctionService.CorrectAsync(
            ContributorContext(),
            correction,
            state,
            catalog,
            new DateTimeOffset(2026, 6, 19, 11, 0, 0, TimeSpan.Zero),
            TestContext.Current.CancellationToken);

        correctionResult.WasDispatched.ShouldBeTrue();
        correctionResult.HasAcceptedEvents.ShouldBeTrue();
        TimeEntryCorrected corrected = correctionResult.DomainResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimeEntryCorrected>();
        corrected.TimeEntryId.ShouldBe(TimeEntryId());
        corrected.TimeEntryCorrectionId.ShouldBe(correction.TimeEntryCorrectionId);
        corrected.CorrectedBy.ShouldBe(Contributor());
        corrected.Tenant.ShouldBe(Tenant());
        corrected.ApprovalState.ShouldBe(TimeEntryApprovalState.Draft);
        corrected.CorrectionState.ShouldBe(TimeEntryCorrectionState.Corrected);
        corrected.RejectionReason.ShouldBe(rejectionReason);
        corrected.RejectionDecisionId.ShouldBe(rejectionDecisionId);
        corrected.PreviousValues.DurationMinutes.ShouldBe(60);
        corrected.CorrectedValues.DurationMinutes.ShouldBe(75);
        corrected.CorrectedValues.Comment.ShouldBe(correction.Comment);
        state.Apply(corrected);

        TimeEntrySubmitted resubmission = await SubmitOneAsync(
            submissionService,
            ContributorContext(),
            state,
            new TimeEntrySubmissionId("submission-2"),
            new DateTimeOffset(2026, 6, 19, 11, 30, 0, TimeSpan.Zero),
            catalog);

        TimeEntryEvidenceReadModel evidence = await DiscloseEvidenceAsync(
            accessGuard,
            Project(recorded, firstSubmission, rejected, corrected, resubmission));

        evidence.ApprovalState.ShouldBe(TimeEntryApprovalState.Submitted);
        evidence.CorrectionState.ShouldBe(TimeEntryCorrectionState.Corrected);
        evidence.DurationMinutes.ShouldBe(75);
        evidence.Comment.ShouldBe(correction.Comment);
        evidence.ApprovalDecision.ShouldNotBeNull().ShouldSatisfyAllConditions(
            decision => decision.ApprovalState.ShouldBe(TimeEntryApprovalState.Rejected),
            decision => decision.Reason.ShouldBe(rejectionReason));
        evidence.Correction.ShouldNotBeNull().ShouldSatisfyAllConditions(
            correctionEvidence => correctionEvidence.TimeEntryCorrectionId.ShouldBe(correction.TimeEntryCorrectionId),
            correctionEvidence => correctionEvidence.CorrectedBy.ShouldBe(Contributor()),
            correctionEvidence => correctionEvidence.RejectionReason.ShouldBe(rejectionReason),
            correctionEvidence => correctionEvidence.RejectionDecisionId.ShouldBe(rejectionDecisionId),
            correctionEvidence => correctionEvidence.PreviousValues.DurationMinutes.ShouldBe(60),
            correctionEvidence => correctionEvidence.CorrectedValues.DurationMinutes.ShouldBe(75));
        evidence.EventLineage.Select(static item => item.EventName).ShouldBe(
        [
            nameof(TimeEntryRecorded),
            nameof(TimeEntrySubmitted),
            nameof(TimeEntryRejected),
            nameof(TimeEntryCorrected),
            nameof(TimeEntrySubmitted)
        ]);
        evidence.DisplayHydration.Contributor.Label.ShouldBe("Contributor");
        evidence.DisplayHydration.Target.Label.ShouldBe("Project");
        evidence.DisplayHydration.ActivityType.Label.ShouldBe("Delivery");
        authorityProvider.Requests.Select(static request => request.Action).ShouldContain(ApprovalAuthorityAction.CorrectionAuthorization);
        accessGuard.Requests.Count(static request => request.Operation == TimesheetsOperation.ProjectionRead).ShouldBe(2);

        string json = JsonSerializer.Serialize(evidence, JsonOptions);

        json.ShouldContain("\"approvalState\":\"Submitted\"");
        json.ShouldContain("\"correctionState\":\"Corrected\"");
        json.ShouldContain("Needs customer PO evidence.");
        json.ShouldContain("Corrected after rejection.");
        AssertJsonOmitsCallerAndEnvelopeAuthority(json);
    }

    [Fact]
    public async Task Correction_workflow_fails_closed_before_dispatch_when_corrected_target_is_not_authorized()
    {
        ConfigurableAccessGuard accessGuard = new()
        {
            DenyCorrectedTarget = true
        };
        FixedAuthorityProvider authorityProvider = AllowingAuthorityProvider();
        TimeEntryCorrectionCommandService correctionService = CorrectionService(accessGuard, authorityProvider);

        TimeEntryCorrectionCommandResult result = await correctionService.CorrectAsync(
            ContributorContext(),
            CorrectionCommand(),
            RejectedState(),
            FreshCatalog(ActiveCatalogItem(ActivityId())),
            new DateTimeOffset(2026, 6, 19, 11, 0, 0, TimeSpan.Zero),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeFalse();
        result.HasAcceptedEvents.ShouldBeFalse();
        result.HasCorrectedAuthorizationDenial.ShouldBeTrue();
        result.DomainResult.ShouldBeNull();
        result.AuthorityResolution.ShouldBeNull();
        result.CorrectedAuthorization.ShouldNotBeNull().ShouldSatisfyAllConditions(
            authorization => authorization.DenialCategory.ShouldBe(TimesheetsDenialCategory.CrossTenantTarget),
            authorization => authorization.Reason.ShouldBe("Authority cannot be resolved."),
            authorization => authorization.Reason.ShouldNotContain("tenant-2", Case.Insensitive),
            authorization => authorization.Reason.ShouldNotContain("project-2", Case.Insensitive),
            authorization => authorization.Reason.ShouldNotContain("Needs customer PO evidence.", Case.Insensitive));
        accessGuard.Requests.Select(static request => request.Operation).ShouldBe(
        [
            TimesheetsOperation.Command,
            TimesheetsOperation.Command
        ]);
        authorityProvider.Requests.ShouldBeEmpty();

        string json = JsonSerializer.Serialize(result.CorrectedAuthorization, JsonOptions);

        json.ShouldContain("Authority cannot be resolved.");
        json.ShouldNotContain("tenant-2");
        json.ShouldNotContain("project-2");
        AssertJsonOmitsCallerAndEnvelopeAuthority(json);
    }

    [Fact]
    public async Task Correction_workflow_fails_closed_before_dispatch_when_activity_type_catalog_is_stale()
    {
        ConfigurableAccessGuard accessGuard = new();
        FixedAuthorityProvider authorityProvider = AllowingAuthorityProvider();
        TimeEntryCorrectionCommandService correctionService = CorrectionService(accessGuard, authorityProvider);

        TimeEntryCorrectionCommandResult result = await correctionService.CorrectAsync(
            ContributorContext(),
            CorrectionCommand(),
            RejectedState(),
            new ActivityTypeCatalogReadModel([], ProjectionFreshnessMetadata.Stale()),
            new DateTimeOffset(2026, 6, 19, 11, 0, 0, TimeSpan.Zero),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeFalse();
        result.HasAcceptedEvents.ShouldBeFalse();
        result.AggregateDispatched.ShouldBeFalse();
        result.HasCorrectionPolicyDenial.ShouldBeFalse();
        result.CurrentAuthorization.IsAuthorized.ShouldBeTrue();
        result.CorrectedAuthorization.ShouldNotBeNull().IsAuthorized.ShouldBeTrue();
        authorityProvider.Requests.ShouldHaveSingleItem().Action.ShouldBe(ApprovalAuthorityAction.CorrectionAuthorization);

        TimesheetsRejection rejection = result.DomainResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimesheetsRejection>();
        rejection.Code.ShouldBe(TimesheetsRejectionCode.ProjectionUnavailable);
        rejection.Message.ShouldBe("Activity Type catalog is not fresh enough for correction.");
        rejection.FieldErrors.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            error => error.Field.ShouldBe("activityTypeCatalog"),
            error => error.Code.ShouldBe("not-fresh"));
        accessGuard.Requests.Select(static request => request.Operation).ShouldBe(
        [
            TimesheetsOperation.Command,
            TimesheetsOperation.Command
        ]);

        string json = JsonSerializer.Serialize(result.DomainResult, JsonOptions);

        json.ShouldContain("Activity Type catalog is not fresh enough for correction.");
        AssertJsonOmitsCallerAndEnvelopeAuthority(json);
    }

    private static async ValueTask<TimeEntrySubmitted> SubmitOneAsync(
        TimeEntrySubmissionCommandService submissionService,
        TimesheetsRequestContext context,
        TimeEntryState state,
        TimeEntrySubmissionId submissionId,
        DateTimeOffset submittedAtUtc,
        ActivityTypeCatalogReadModel catalog)
    {
        TimeEntrySubmissionCommandResult result = await submissionService.SubmitAsync(
            context,
            new SubmitTimeEntriesForApproval(
                submissionId,
                [TimeEntryId()],
                TimeEntrySubmissionScope.SelectedEntries),
            new Dictionary<TimeEntryId, TimeEntryState?> { [TimeEntryId()] = state },
            catalog,
            submittedAtUtc,
            TestContext.Current.CancellationToken);

        result.HasAcceptedEvents.ShouldBeTrue();
        result.HasBlockedEntries.ShouldBeFalse();
        return result.Entries.ShouldHaveSingleItem()
            .DomainResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimeEntrySubmitted>();
    }

    private static TimeEntryEvidenceReadModel Project(
        TimeEntryRecorded recorded,
        TimeEntrySubmitted firstSubmission,
        TimeEntryRejected rejected,
        TimeEntryCorrected corrected,
        TimeEntrySubmitted resubmission)
        => new TimeEntryEvidenceProjection().Project(
            Tenant().TenantId,
            TimeEntryId(),
            [
                new("message-5", 5, resubmission),
                new("message-2", 2, firstSubmission),
                new("message-4", 4, corrected),
                new("message-3", 3, rejected),
                new("message-1", 1, recorded),
                new("message-4", 4, corrected)
            ],
            new(Tenant().TenantId, TimeEntryEvidenceProjection.ProjectionName, 5, ProjectionFreshness.Fresh))
            .ShouldNotBeNull();

    private static async ValueTask<TimeEntryEvidenceReadModel> DiscloseEvidenceAsync(
        ConfigurableAccessGuard accessGuard,
        TimeEntryEvidenceReadModel projected)
    {
        TimeEntryEvidenceQueryService queryService = new(
            accessGuard,
            new FixedProjectionReader(projected),
            new FixedDisplayHydrator());

        TimeEntryEvidenceQueryResult queryResult = await queryService.ReadAsync(
            ContributorContext(),
            TimeEntryId(),
            TestContext.Current.CancellationToken);

        queryResult.WasDisclosed.ShouldBeTrue();
        return queryResult.Evidence.ShouldNotBeNull();
    }

    private static TimeEntryState StateFrom(TimeEntryRecorded recorded)
    {
        TimeEntryState state = new();
        state.Apply(recorded);
        return state;
    }

    private static TimeEntryState RejectedState()
    {
        TimeEntryState state = new();
        state.Apply(Recorded());
        state.Apply(Submitted(new TimeEntrySubmissionId("submission-1")));
        state.Apply(Rejected());
        return state;
    }

    private static RecordTimeEntry RecordCommand()
        => new(
            TimeEntryId(),
            TimeEntryTargetReference.ForProject(Project()),
            Contributor(),
            ActivityId(),
            new DateOnly(2026, 6, 19),
            60,
            BillableState.Billable,
            ContributorCategory.Employee,
            AiEffortMetrics.Unavailable)
        {
            Comment = new("Original evidence.", TimeEntryCommentPolicy.SensitiveDefault)
        };

    private static CorrectRejectedTimeEntry CorrectionCommand()
        => new(
            TimeEntryId(),
            new TimeEntryCorrectionId("correction-1"),
            TimeEntryTargetReference.ForProject(Project()),
            Contributor(),
            ActivityId(),
            new DateOnly(2026, 6, 20),
            75,
            BillableState.Billable,
            ContributorCategory.Employee,
            AiEffortMetrics.Unavailable)
        {
            Comment = new("Corrected after rejection.", TimeEntryCommentPolicy.SensitiveDefault)
        };

    private static TimeEntryRecorded Recorded() => new(
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
        AiEffortMetrics.Unavailable)
    {
        Comment = new("Original evidence.", TimeEntryCommentPolicy.SensitiveDefault)
    };

    private static TimeEntrySubmitted Submitted(TimeEntrySubmissionId submissionId)
        => new(
            TimeEntryId(),
            Contributor(),
            Tenant(),
            new DateTimeOffset(2026, 6, 19, 8, 0, 0, TimeSpan.Zero),
            submissionId,
            TimeEntrySubmissionScope.SelectedEntries,
            TimeEntryApprovalState.Submitted);

    private static TimeEntryRejected Rejected()
        => new(
            TimeEntryId(),
            Approver(),
            Tenant(),
            new DateTimeOffset(2026, 6, 19, 10, 0, 0, TimeSpan.Zero),
            new TimeEntryApprovalDecisionId("rejection-decision-1"),
            TimeEntryApprovalState.Rejected,
            new(
                ApprovalAuthorityAction.EntryRejection,
                ApprovalAuthoritySource.ProjectApprover,
                ApprovalAuthorityDecisionState.Allowed,
                TimesheetsApprovalAuthorityPolicyOptions.DefaultPolicyKey,
                "v2",
                ProjectionFreshnessMetadata.Fresh),
            TimeEntryApprovalScope.IndividualEntry,
            new TimeEntryRejectionReason("Needs customer PO evidence."));

    private static TimeEntryApprovalCommandService ApprovalService(
        ConfigurableAccessGuard accessGuard,
        FixedAuthorityProvider authorityProvider)
        => new(accessGuard, AuthorityResolver(authorityProvider));

    private static TimeEntryCorrectionCommandService CorrectionService(
        ConfigurableAccessGuard accessGuard,
        FixedAuthorityProvider authorityProvider)
        => new(accessGuard, AuthorityResolver(authorityProvider));

    private static TimesheetsApprovalAuthorityResolver AuthorityResolver(FixedAuthorityProvider authorityProvider)
        => new(
            new TimesheetsApprovalAuthorityPolicyOptions
            {
                PolicyVersion = "v2"
            },
            [authorityProvider]);

    private static FixedAuthorityProvider AllowingAuthorityProvider()
        => new(static request => ApprovalAuthoritySourceResult.Allowed(
            request.Action == ApprovalAuthorityAction.CorrectionAuthorization
                ? ApprovalAuthoritySource.TenantAdministrator
                : ApprovalAuthoritySource.ProjectApprover,
            ProjectionFreshnessMetadata.Fresh));

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

    private static TimesheetsRequestContext ContributorContext()
        => new(Tenant(), Contributor(), "correlation-contributor");

    private static TimesheetsRequestContext ApproverContext()
        => new(Tenant(), Approver(), "correlation-approver");

    private static TenantReference Tenant() => new("tenant-1");

    private static PartyReference Contributor() => new("party-contributor");

    private static PartyReference Approver() => new("party-approver");

    private static ProjectReference Project() => new("project-1");

    private static ActivityTypeId ActivityId() => new("activity-type-1");

    private static TimeEntryId TimeEntryId() => new("time-entry-1");

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
        Func<ApprovalAuthorityResolutionRequest, ApprovalAuthoritySourceResult> evaluate)
        : IApprovalAuthoritySourceProvider
    {
        public List<ApprovalAuthorityResolutionRequest> Requests { get; } = [];

        public ApprovalAuthoritySource Source => ApprovalAuthoritySource.ProjectApprover;

        public int Precedence => TimesheetsApprovalAuthorityPolicyOptions.DefaultPrecedence(Source);

        public ValueTask<ApprovalAuthoritySourceResult> EvaluateAsync(
            ApprovalAuthorityResolutionRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return ValueTask.FromResult(evaluate(request));
        }
    }

    private sealed class ConfigurableAccessGuard : ITimesheetsAccessGuard
    {
        public List<TimesheetsAuthorizationRequest> Requests { get; } = [];

        public bool DenyCorrectedTarget { get; init; }

        public ValueTask<TimesheetsAuthorizationDecision> AuthorizeAsync(
            TimesheetsAuthorizationRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);

            if (DenyCorrectedTarget
                && Requests.Count == 2
                && request.Project == Project()
                && request.Contributor == Contributor())
            {
                return ValueTask.FromResult(TimesheetsAuthorizationDecision.Denied(
                    TimesheetsDenialCategory.CrossTenantTarget,
                    "tenant-2 project-2 Needs customer PO evidence."));
            }

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
                request.UiAction ?? TimesheetsUiAction.Correction));
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
