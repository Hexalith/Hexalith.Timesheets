using System.Text.Json;
using System.Text.Json.Nodes;

using Hexalith.Timesheets.Contracts;
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

public sealed class ApprovedEntryCorrectionsE2ETests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Approved_time_entry_correction_workflow_appends_lineage_projects_effective_values_and_keeps_safe_add_correction_vocabulary()
    {
        ConfigurableAccessGuard accessGuard = new();
        FixedAuthorityProvider authorityProvider = AllowingAuthorityProvider();
        TimeEntryCommandService recordService = new(accessGuard);
        TimeEntrySubmissionCommandService submissionService = new(accessGuard);
        TimeEntryApprovalCommandService approvalService = ApprovalService(accessGuard, authorityProvider);
        TimeEntryCorrectionCommandService correctionService = CorrectionService(accessGuard, authorityProvider);
        ActivityTypeCatalogReadModel catalog = FreshCatalog(ActiveCatalogItem(ActivityId()));
        RecordTimeEntry record = RecordCommand();

        TimeEntryRecorded recorded = (await recordService.RecordAsync(
            ContributorContext(),
            record,
            null,
            catalog,
            TestContext.Current.CancellationToken))
            .DomainResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimeEntryRecorded>();
        TimeEntryState state = StateFrom(recorded);

        TimeEntrySubmitted submitted = await SubmitOneAsync(
            submissionService,
            ContributorContext(),
            state,
            catalog);
        state.Apply(submitted);

        TimeEntryApprovalDecisionId approvalDecisionId = new("approval-decision-1");
        TimeEntryApproved approved = (await approvalService.ApproveAsync(
            ReviewerContext(),
            new ApproveTimeEntry(TimeEntryId(), approvalDecisionId),
            state,
            ApprovedAtUtc(),
            TestContext.Current.CancellationToken))
            .DomainResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimeEntryApproved>();
        state.Apply(approved);

        CorrectApprovedTimeEntry correction = ApprovedCorrectionCommand(Project());
        TimeEntryCorrectionCommandResult correctionResult = await correctionService.CorrectAsync(
            ReviewerContext(),
            correction,
            state,
            catalog,
            CorrectedAtUtc(),
            TestContext.Current.CancellationToken);

        correctionResult.WasDispatched.ShouldBeTrue();
        correctionResult.HasAcceptedEvents.ShouldBeTrue();
        TimeEntryApprovedCorrected corrected = correctionResult.DomainResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimeEntryApprovedCorrected>();
        corrected.TimeEntryId.ShouldBe(TimeEntryId());
        corrected.TimeEntryCorrectionId.ShouldBe(correction.TimeEntryCorrectionId);
        corrected.CorrectedBy.ShouldBe(Reviewer());
        corrected.Tenant.ShouldBe(Tenant());
        corrected.CorrectedAtUtc.ShouldBe(CorrectedAtUtc());
        corrected.Reason.ShouldBe(correction.Reason);
        corrected.SourceApprovalDecisionId.ShouldBe(approvalDecisionId);
        corrected.SourceApprovalScope.ShouldBe(TimeEntryApprovalScope.IndividualEntry);
        corrected.ApprovalState.ShouldBe(TimeEntryApprovalState.Approved);
        corrected.CorrectionState.ShouldBe(TimeEntryCorrectionState.Corrected);
        corrected.PreviousValues.DurationMinutes.ShouldBe(60);
        corrected.CorrectedValues.DurationMinutes.ShouldBe(75);
        corrected.CorrectedValues.Comment.ShouldBe(correction.Comment);
        state.Apply(corrected);
        state.ApprovalState.ShouldBe(TimeEntryApprovalState.Approved);
        state.LockState.ShouldBe(TimeEntryLockState.LockedFromDirectEdit);

        TimeEntryEvidenceReadModel evidence = await DiscloseEvidenceAsync(
            accessGuard,
            Project(recorded, submitted, approved, corrected));

        evidence.ApprovalState.ShouldBe(TimeEntryApprovalState.Approved);
        evidence.CorrectionState.ShouldBe(TimeEntryCorrectionState.Corrected);
        evidence.DurationMinutes.ShouldBe(75);
        evidence.Comment.ShouldBe(correction.Comment);
        evidence.Correction.ShouldBeNull();
        evidence.ApprovalDecision.ShouldNotBeNull().ShouldSatisfyAllConditions(
            decision => decision.TimeEntryApprovalDecisionId.ShouldBe(approvalDecisionId),
            decision => decision.ApprovalState.ShouldBe(TimeEntryApprovalState.Approved),
            decision => decision.Approver.ShouldBe(Reviewer()));
        evidence.ApprovedCorrection.ShouldNotBeNull().ShouldSatisfyAllConditions(
            correctionEvidence => correctionEvidence.TimeEntryCorrectionId.ShouldBe(correction.TimeEntryCorrectionId),
            correctionEvidence => correctionEvidence.CorrectedBy.ShouldBe(Reviewer()),
            correctionEvidence => correctionEvidence.Reason.ShouldBe(correction.Reason),
            correctionEvidence => correctionEvidence.SourceApprovalDecisionId.ShouldBe(approvalDecisionId),
            correctionEvidence => correctionEvidence.PreviousValues.DurationMinutes.ShouldBe(60),
            correctionEvidence => correctionEvidence.CorrectedValues.DurationMinutes.ShouldBe(75),
            correctionEvidence => correctionEvidence.ApprovalState.ShouldBe(TimeEntryApprovalState.Approved));
        evidence.LockEvidence.ShouldSatisfyAllConditions(
            lockEvidence => lockEvidence.LockState.ShouldBe(TimeEntryLockState.LockedFromDirectEdit),
            lockEvidence => lockEvidence.SourceApprovalDecisionId.ShouldBe(approvalDecisionId),
            lockEvidence => lockEvidence.LockedBy.ShouldBe(Reviewer()));
        evidence.EventLineage.Select(static item => item.EventName).ShouldBe(
        [
            nameof(TimeEntryRecorded),
            nameof(TimeEntrySubmitted),
            nameof(TimeEntryApproved),
            nameof(TimeEntryApprovedCorrected)
        ]);
        evidence.DisplayHydration.Contributor.Label.ShouldBe("Contributor");
        evidence.DisplayHydration.Target.Label.ShouldBe("Project");
        evidence.DisplayHydration.ActivityType.Label.ShouldBe("Delivery");
        authorityProvider.Requests.Select(static request => request.Action).ShouldBe(
        [
            ApprovalAuthorityAction.EntryApproval,
            ApprovalAuthorityAction.CorrectionAuthorization
        ]);

        AssertMetadataAndOpenApiExposeApprovedCorrectionWithoutDirectEditCopy();

        string json = JsonSerializer.Serialize(evidence, JsonOptions);

        json.ShouldContain("\"approvalState\":\"Approved\"");
        json.ShouldContain("\"correctionState\":\"Corrected\"");
        json.ShouldContain("\"lockState\":\"LockedFromDirectEdit\"");
        json.ShouldContain("Correct approved duration after audit review.");
        json.ShouldContain("Corrected approved evidence.");
        AssertJsonOmitsCallerAndEnvelopeAuthority(json);
    }

    [Fact]
    public async Task Approved_correction_workflow_fails_closed_before_dispatch_when_corrected_target_is_not_authorized()
    {
        ConfigurableAccessGuard accessGuard = new()
        {
            DenyCorrectedTarget = true
        };
        FixedAuthorityProvider authorityProvider = AllowingAuthorityProvider();
        TimeEntryCorrectionCommandService correctionService = CorrectionService(accessGuard, authorityProvider);

        TimeEntryCorrectionCommandResult result = await correctionService.CorrectAsync(
            ReviewerContext(),
            ApprovedCorrectionCommand(new ProjectReference("project-2")),
            ApprovedState(),
            FreshCatalog(ActiveCatalogItem(ActivityId())),
            CorrectedAtUtc(),
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
            authorization => authorization.Reason.ShouldNotContain("Correct approved duration", Case.Insensitive));
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
    public async Task Approved_correction_workflow_fails_closed_before_dispatch_when_activity_type_catalog_is_stale()
    {
        ConfigurableAccessGuard accessGuard = new();
        FixedAuthorityProvider authorityProvider = AllowingAuthorityProvider();
        TimeEntryCorrectionCommandService correctionService = CorrectionService(accessGuard, authorityProvider);

        TimeEntryCorrectionCommandResult result = await correctionService.CorrectAsync(
            ReviewerContext(),
            ApprovedCorrectionCommand(Project()),
            ApprovedState(),
            new ActivityTypeCatalogReadModel([], ProjectionFreshnessMetadata.Stale()),
            CorrectedAtUtc(),
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

        string json = JsonSerializer.Serialize(result.DomainResult, JsonOptions);

        json.ShouldContain("Activity Type catalog is not fresh enough for correction.");
        AssertJsonOmitsCallerAndEnvelopeAuthority(json);
    }

    private static async ValueTask<TimeEntrySubmitted> SubmitOneAsync(
        TimeEntrySubmissionCommandService submissionService,
        TimesheetsRequestContext context,
        TimeEntryState state,
        ActivityTypeCatalogReadModel catalog)
    {
        TimeEntrySubmissionCommandResult result = await submissionService.SubmitAsync(
            context,
            new SubmitTimeEntriesForApproval(
                new TimeEntrySubmissionId("submission-1"),
                [TimeEntryId()],
                TimeEntrySubmissionScope.SelectedEntries),
            new Dictionary<TimeEntryId, TimeEntryState?> { [TimeEntryId()] = state },
            catalog,
            SubmittedAtUtc(),
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
        TimeEntrySubmitted submitted,
        TimeEntryApproved approved,
        TimeEntryApprovedCorrected corrected)
        => new TimeEntryEvidenceProjection().Project(
            Tenant().TenantId,
            TimeEntryId(),
            [
                new("message-4", 4, corrected),
                new("message-2", 2, submitted),
                new("message-3", 3, approved),
                new("message-1", 1, recorded),
                new("message-4", 4, corrected)
            ],
            new(Tenant().TenantId, TimeEntryEvidenceProjection.ProjectionName, 4, ProjectionFreshness.Fresh))
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

    private static TimeEntryState ApprovedState()
    {
        TimeEntryState state = new();
        state.Apply(Recorded());
        state.Apply(Submitted());
        state.Apply(Approved());
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
            Comment = new("Approved evidence.", TimeEntryCommentPolicy.SensitiveDefault)
        };

    private static CorrectApprovedTimeEntry ApprovedCorrectionCommand(ProjectReference project)
        => new(
            TimeEntryId(),
            new TimeEntryCorrectionId("approved-correction-1"),
            TimeEntryTargetReference.ForProject(project),
            Contributor(),
            ActivityId(),
            new DateOnly(2026, 6, 20),
            75,
            BillableState.Billable,
            ContributorCategory.Employee,
            AiEffortMetrics.Unavailable,
            new TimeEntryCorrectionReason("Correct approved duration after audit review."))
        {
            Comment = new("Corrected approved evidence.", TimeEntryCommentPolicy.SensitiveDefault)
        };

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
            AiEffortMetrics.Unavailable)
        {
            Comment = new("Approved evidence.", TimeEntryCommentPolicy.SensitiveDefault)
        };

    private static TimeEntrySubmitted Submitted()
        => new(
            TimeEntryId(),
            Contributor(),
            Tenant(),
            SubmittedAtUtc(),
            new TimeEntrySubmissionId("submission-1"),
            TimeEntrySubmissionScope.SelectedEntries,
            TimeEntryApprovalState.Submitted);

    private static TimeEntryApproved Approved()
        => new(
            TimeEntryId(),
            Reviewer(),
            Tenant(),
            ApprovedAtUtc(),
            new TimeEntryApprovalDecisionId("approval-decision-1"),
            TimeEntryApprovalState.Approved,
            new(
                ApprovalAuthorityAction.EntryApproval,
                ApprovalAuthoritySource.ProjectApprover,
                ApprovalAuthorityDecisionState.Allowed,
                TimesheetsApprovalAuthorityPolicyOptions.DefaultPolicyKey,
                "v2",
                ProjectionFreshnessMetadata.Fresh),
            TimeEntryApprovalScope.IndividualEntry);

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

    private static TimesheetsRequestContext ReviewerContext()
        => new(Tenant(), Reviewer(), "correlation-reviewer");

    private static TenantReference Tenant() => new("tenant-1");

    private static PartyReference Contributor() => new("party-contributor");

    private static PartyReference Reviewer() => new("party-reviewer");

    private static ProjectReference Project() => new("project-1");

    private static ActivityTypeId ActivityId() => new("activity-type-1");

    private static TimeEntryId TimeEntryId() => new("time-entry-1");

    private static DateTimeOffset SubmittedAtUtc()
        => new(2026, 6, 19, 8, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset ApprovedAtUtc()
        => new(2026, 6, 19, 10, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset CorrectedAtUtc()
        => new(2026, 6, 19, 11, 0, 0, TimeSpan.Zero);

    private static void AssertMetadataAndOpenApiExposeApprovedCorrectionWithoutDirectEditCopy()
    {
        var correctionDescriptor = TimesheetsMetadataCatalog.Descriptors
            .Single(static descriptor => descriptor.Name == "timesheets.command.correct-approved-time-entry");
        correctionDescriptor.Title.ShouldBe("Add correction");
        correctionDescriptor.Actions.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            action => action.Label.ShouldBe("Add correction"),
            action => action.Intent.ShouldBe("Timesheets.CorrectApprovedTimeEntry"));
        correctionDescriptor.Title.ShouldNotContain("Edit approved entry", Case.Insensitive);
        correctionDescriptor.Actions.Select(static action => action.Label)
            .ShouldNotContain("Edit approved entry");

        var evidenceDescriptor = TimesheetsMetadataCatalog.Descriptors
            .Single(static descriptor => descriptor.Name == "timesheets.projection.time-entry-evidence");
        evidenceDescriptor.Fields.ShouldContain(static field =>
            field.Name == "approvedCorrection"
            && field.ContractType == nameof(TimeEntryApprovedCorrectionEvidence));
        evidenceDescriptor.Actions.ShouldContain(static action =>
            action.Name == "add-correction"
            && action.Label == "Add correction"
            && action.Intent == "Timesheets.CorrectApprovedTimeEntry");
        evidenceDescriptor.Actions.Select(static action => action.Label)
            .ShouldNotContain("Edit approved entry");

        string openApi = File.ReadAllText(TestRepositoryRoot.PathTo(
            "src",
            "Hexalith.Timesheets.Contracts",
            "openapi",
            "timesheets-capture-contracts.v1.json"));
        JsonObject schemas = JsonNode.Parse(openApi).ShouldNotBeNull()["components"]!["schemas"]!.AsObject();

        schemas.ContainsKey("CorrectApprovedTimeEntry").ShouldBeTrue();
        schemas.ContainsKey("TimeEntryApprovedCorrected").ShouldBeTrue();
        schemas.ContainsKey("TimeEntryApprovedCorrectionEvidence").ShouldBeTrue();
        openApi.ShouldNotContain("Edit approved entry", Case.Insensitive);
    }

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
                && request.Project == new ProjectReference("project-2")
                && request.Contributor == Contributor())
            {
                return ValueTask.FromResult(TimesheetsAuthorizationDecision.Denied(
                    TimesheetsDenialCategory.CrossTenantTarget,
                    "tenant-2 project-2 Correct approved duration after audit review."));
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
