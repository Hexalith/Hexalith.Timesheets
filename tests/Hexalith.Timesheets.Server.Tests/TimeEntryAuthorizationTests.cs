using Hexalith.Timesheets.Contracts.Commands.TimeEntries;
using Hexalith.Timesheets.Contracts.Events.Rejections;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.ApprovalAuthority;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.References;
using Hexalith.Timesheets.Server.TimeEntries;

using NSubstitute;

using Shouldly;

namespace Hexalith.Timesheets.Server.Tests;

public sealed class TimeEntryAuthorizationTests
{
    [Theory]
    [InlineData(TimesheetsTenantAccessState.MissingTenant, TimesheetsDenialCategory.MissingTenant)]
    [InlineData(TimesheetsTenantAccessState.NonMember, TimesheetsDenialCategory.NonMember)]
    [InlineData(TimesheetsTenantAccessState.StaleProjection, TimesheetsDenialCategory.StaleProjection)]
    [InlineData(TimesheetsTenantAccessState.AmbiguousAuthority, TimesheetsDenialCategory.AmbiguousAuthority)]
    [InlineData(TimesheetsTenantAccessState.UnavailableSiblingAuthority, TimesheetsDenialCategory.UnavailableSiblingAuthority)]
    public async Task Capture_fails_closed_on_tenant_authority_before_target_contributor_policy_or_domain_dispatch(
        TimesheetsTenantAccessState accessState,
        TimesheetsDenialCategory expectedCategory)
    {
        Fixture fixture = new();
        fixture.TenantValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), TimesheetsOperation.Command, Arg.Any<CancellationToken>())
            .Returns(TimesheetsTenantAccessResult.Denied(accessState, "Authority cannot be resolved."));

        TimeEntryCommandResult result = await fixture.CreateService().RecordAsync(
            Context(),
            ProjectCommand(),
            null,
            FreshCatalog(ActivityTypeScope.Tenant),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeFalse();
        result.DomainResult.ShouldBeNull();
        result.Authorization.DenialCategory.ShouldBe(expectedCategory);
        fixture.ProjectValidator.ReceivedCalls().ShouldBeEmpty();
        fixture.WorkValidator.ReceivedCalls().ShouldBeEmpty();
        fixture.PartyValidator.ReceivedCalls().ShouldBeEmpty();
        fixture.PolicyEvaluator.ReceivedCalls().ShouldBeEmpty();
    }

    [Theory]
    [InlineData(ReferenceValidationState.TenantMismatch, TimesheetsDenialCategory.CrossTenantTarget)]
    [InlineData(ReferenceValidationState.Stale, TimesheetsDenialCategory.StaleProjection)]
    [InlineData(ReferenceValidationState.Ambiguous, TimesheetsDenialCategory.AmbiguousAuthority)]
    [InlineData(ReferenceValidationState.Unavailable, TimesheetsDenialCategory.UnavailableSiblingAuthority)]
    [InlineData(ReferenceValidationState.DisabledOrArchived, TimesheetsDenialCategory.UnavailableSiblingAuthority)]
    [InlineData(ReferenceValidationState.InvalidReference, TimesheetsDenialCategory.InvalidReference)]
    public async Task Project_targeted_capture_fails_closed_before_domain_dispatch_when_project_authority_is_invalid(
        ReferenceValidationState referenceState,
        TimesheetsDenialCategory expectedCategory)
    {
        Fixture fixture = AuthorizedFixture();
        fixture.ProjectValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<ProjectReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Denied(referenceState, "Project authority cannot be resolved."));

        TimeEntryCommandService service = fixture.CreateService();

        TimeEntryCommandResult result = await service.RecordAsync(
            Context(),
            ProjectCommand(),
            null,
            FreshCatalog(ActivityTypeScope.Tenant),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeFalse();
        result.DomainResult.ShouldBeNull();
        result.Authorization.DenialCategory.ShouldBe(expectedCategory);
        await fixture.ProjectValidator.Received(1)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Project(), Arg.Any<CancellationToken>());
        fixture.WorkValidator.ReceivedCalls().ShouldBeEmpty();
        await fixture.PartyValidator.Received(0)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<PartyReference>(), Arg.Any<CancellationToken>());
        fixture.PolicyEvaluator.ReceivedCalls().ShouldBeEmpty();
    }

    [Theory]
    [InlineData(ReferenceValidationState.Unauthorized, TimesheetsDenialCategory.InsufficientRole)]
    [InlineData(ReferenceValidationState.TenantMismatch, TimesheetsDenialCategory.CrossTenantTarget)]
    [InlineData(ReferenceValidationState.Stale, TimesheetsDenialCategory.StaleProjection)]
    [InlineData(ReferenceValidationState.Ambiguous, TimesheetsDenialCategory.AmbiguousAuthority)]
    [InlineData(ReferenceValidationState.Unavailable, TimesheetsDenialCategory.UnavailableSiblingAuthority)]
    [InlineData(ReferenceValidationState.InvalidReference, TimesheetsDenialCategory.InvalidReference)]
    public async Task Capture_fails_closed_on_contributor_authority_before_policy_or_domain_dispatch(
        ReferenceValidationState referenceState,
        TimesheetsDenialCategory expectedCategory)
    {
        Fixture fixture = AuthorizedFixture();
        fixture.ProjectValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<ProjectReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());
        fixture.PartyValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<PartyReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Denied(referenceState, "Contributor authority cannot be resolved."));

        TimeEntryCommandResult result = await fixture.CreateService().RecordAsync(
            Context(),
            ProjectCommand(),
            null,
            FreshCatalog(ActivityTypeScope.Tenant),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeFalse();
        result.DomainResult.ShouldBeNull();
        result.Authorization.DenialCategory.ShouldBe(expectedCategory);
        await fixture.ProjectValidator.Received(1)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Project(), Arg.Any<CancellationToken>());
        await fixture.PartyValidator.Received(1)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Contributor(), Arg.Any<CancellationToken>());
        fixture.PolicyEvaluator.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Project_targeted_capture_validates_tenant_project_contributor_policy_then_dispatches()
    {
        Fixture fixture = AuthorizedFixture();
        fixture.ProjectValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<ProjectReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());
        fixture.PartyValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<PartyReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());

        TimeEntryCommandResult result = await fixture.CreateService().RecordAsync(
            Context(),
            ProjectCommand(),
            null,
            FreshCatalog(ActivityTypeScope.Tenant),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeTrue();
        result.DomainResult.ShouldNotBeNull();
        result.DomainResult.Events.ShouldHaveSingleItem().ShouldBeOfType<TimeEntryRecorded>();
        await fixture.ProjectValidator.Received(1)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Project(), Arg.Any<CancellationToken>());
        fixture.WorkValidator.ReceivedCalls().ShouldBeEmpty();
        await fixture.PartyValidator.Received(1)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Contributor(), Arg.Any<CancellationToken>());
        await fixture.PolicyEvaluator.Received(1)
            .EvaluateAsync(
                Arg.Is<TimesheetsAuthorizationRequest>(request =>
                    request != null
                    && request.Project == Project()
                    && request.Work == null
                    && request.Contributor == Contributor()),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Ai_agent_capture_uses_existing_tenant_project_contributor_policy_and_catalog_gates()
    {
        Fixture fixture = AuthorizedFixture();
        fixture.ProjectValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<ProjectReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());
        fixture.PartyValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<PartyReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());

        TimeEntryCommandResult result = await fixture.CreateService().RecordAsync(
            Context(),
            AiProjectCommand(),
            null,
            FreshCatalog(ActivityTypeScope.Tenant),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeTrue();
        TimeEntryRecorded recorded = result.DomainResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimeEntryRecorded>();
        recorded.ContributorCategory.ShouldBe(ContributorCategory.AutomatedAgent);
        recorded.AiMetrics.ShouldNotBeNull();
        recorded.AiMetrics.TokenAvailability.ShouldBe(AiTokenMetricAvailability.ProviderReported);
        await fixture.ProjectValidator.Received(1)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Project(), Arg.Any<CancellationToken>());
        await fixture.PartyValidator.Received(1)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Contributor(), Arg.Any<CancellationToken>());
        await fixture.PolicyEvaluator.Received(1)
            .EvaluateAsync(Arg.Any<TimesheetsAuthorizationRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Capture_policy_denial_fails_before_activity_type_selection_or_domain_dispatch()
    {
        Fixture fixture = AuthorizedFixture();
        fixture.ProjectValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<ProjectReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());
        fixture.PartyValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<PartyReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());
        fixture.PolicyEvaluator
            .EvaluateAsync(Arg.Any<TimesheetsAuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(TimesheetsPolicyEvaluationResult.Denied(
                TimesheetsDenialCategory.CommentPolicyMissing,
                "Comment policy is not resolved."));

        TimeEntryCommandResult result = await fixture.CreateService().RecordAsync(
            Context(),
            ProjectCommand(),
            null,
            FreshCatalog(ActivityTypeScope.Tenant),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeFalse();
        result.DomainResult.ShouldBeNull();
        result.Authorization.DenialCategory.ShouldBe(TimesheetsDenialCategory.CommentPolicyMissing);
    }

    [Fact]
    public async Task Work_targeted_capture_validates_only_work_and_contributor_before_policy()
    {
        Fixture fixture = AuthorizedFixture();
        fixture.WorkValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<WorkReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());
        fixture.PartyValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<PartyReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());

        TimeEntryCommandResult result = await fixture.CreateService().RecordAsync(
            Context(),
            WorkCommand(),
            null,
            FreshCatalog(ActivityTypeScope.Tenant),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeTrue();
        fixture.ProjectValidator.ReceivedCalls().ShouldBeEmpty();
        await fixture.WorkValidator.Received(1)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Work(), Arg.Any<CancellationToken>());
        await fixture.PartyValidator.Received(1)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Contributor(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Capture_rejects_stale_activity_type_catalog_before_aggregate_dispatch()
    {
        Fixture fixture = AuthorizedFixture();
        fixture.ProjectValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<ProjectReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());
        fixture.PartyValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<PartyReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());

        TimeEntryCommandResult result = await fixture.CreateService().RecordAsync(
            Context(),
            ProjectCommand(),
            null,
            new([], ProjectionFreshnessMetadata.Stale("42")),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeFalse();
        result.DomainResult.ShouldNotBeNull();
        result.DomainResult.IsRejection.ShouldBeTrue();
    }

    [Fact]
    public async Task Capture_rejects_missing_activity_type_before_aggregate_dispatch()
    {
        Fixture fixture = AuthorizedProjectFixture();

        TimeEntryCommandResult result = await fixture.CreateService().RecordAsync(
            Context(),
            ProjectCommand(),
            null,
            Catalog(),
            TestContext.Current.CancellationToken);

        TimesheetsRejection rejection = Rejection(result);
        result.WasDispatched.ShouldBeFalse();
        rejection.Code.ShouldBe(TimesheetsRejectionCode.ActivityTypeNotFound);
        rejection.FieldErrors.ShouldContain(static error => error.Field == "activityTypeId" && error.Code == "not-found");
    }

    [Fact]
    public async Task Capture_rejects_inactive_activity_type_before_aggregate_dispatch()
    {
        Fixture fixture = AuthorizedProjectFixture();

        TimeEntryCommandResult result = await fixture.CreateService().RecordAsync(
            Context(),
            ProjectCommand(),
            null,
            Catalog(Item(ActivityId(), ActivityTypeScope.Tenant, null, false)),
            TestContext.Current.CancellationToken);

        TimesheetsRejection rejection = Rejection(result);
        result.WasDispatched.ShouldBeFalse();
        rejection.Code.ShouldBe(TimesheetsRejectionCode.ActivityTypeInactive);
        rejection.FieldErrors.ShouldContain(static error => error.Field == "activityTypeId" && error.Code == "unavailable");
    }

    [Fact]
    public async Task Project_capture_rejects_project_activity_type_from_different_project_before_aggregate_dispatch()
    {
        Fixture fixture = AuthorizedProjectFixture();

        TimeEntryCommandResult result = await fixture.CreateService().RecordAsync(
            Context(),
            ProjectCommand(),
            null,
            Catalog(Item(ActivityId(), ActivityTypeScope.Project, new ProjectReference("other-project"), true)),
            TestContext.Current.CancellationToken);

        TimesheetsRejection rejection = Rejection(result);
        result.WasDispatched.ShouldBeFalse();
        rejection.Code.ShouldBe(TimesheetsRejectionCode.ActivityTypeScopeMismatch);
        rejection.FieldErrors.ShouldContain(static error => error.Field == "activityTypeId" && error.Code == "scope-mismatch");
    }

    [Fact]
    public async Task Work_capture_rejects_project_activity_type_until_governing_project_adapter_exists()
    {
        Fixture fixture = AuthorizedFixture();
        fixture.WorkValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<WorkReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());
        fixture.PartyValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<PartyReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());

        TimeEntryCommandResult result = await fixture.CreateService().RecordAsync(
            Context(),
            WorkCommand(),
            null,
            Catalog(Item(ActivityId(), ActivityTypeScope.Project, Project(), true)),
            TestContext.Current.CancellationToken);

        TimesheetsRejection rejection = Rejection(result);
        result.WasDispatched.ShouldBeFalse();
        rejection.Code.ShouldBe(TimesheetsRejectionCode.AuthorityCannotBeResolved);
        rejection.FieldErrors.ShouldContain(static error => error.Field == "target" && error.Code == "work-project-unresolved");
        fixture.ProjectValidator.ReceivedCalls().ShouldBeEmpty();
        await fixture.WorkValidator.Received(1)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Work(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Project_capture_dispatches_with_matching_project_activity_type_scope()
    {
        Fixture fixture = AuthorizedProjectFixture();

        TimeEntryCommandResult result = await fixture.CreateService().RecordAsync(
            Context(),
            ProjectCommand(),
            null,
            Catalog(Item(ActivityId(), ActivityTypeScope.Project, Project(), true)),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeTrue();
        TimeEntryRecorded recorded = result.DomainResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimeEntryRecorded>();
        recorded.ActivityTypeScope.ShouldBe(ActivityTypeScope.Project);
    }

    [Fact]
    public async Task Submission_fails_closed_on_tenant_authority_before_reference_policy_catalog_or_domain_dispatch()
    {
        Fixture fixture = new();
        fixture.TenantValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), TimesheetsOperation.Command, Arg.Any<CancellationToken>())
            .Returns(TimesheetsTenantAccessResult.Denied(
                TimesheetsTenantAccessState.MissingTenant,
                "Authority cannot be resolved."));

        TimeEntrySubmissionCommandResult result = await fixture.CreateSubmissionService().SubmitAsync(
            Context(),
            SubmitCommand(TimeEntryId()),
            States(RecordedState(ProjectCommand())),
            FreshCatalog(ActivityTypeScope.Tenant),
            SubmittedAtUtc(),
            TestContext.Current.CancellationToken);

        TimeEntrySubmissionEntryResult entry = result.Entries.ShouldHaveSingleItem();
        entry.WasDispatched.ShouldBeFalse();
        entry.DomainResult.ShouldBeNull();
        entry.Authorization.DenialCategory.ShouldBe(TimesheetsDenialCategory.MissingTenant);
        fixture.ProjectValidator.ReceivedCalls().ShouldBeEmpty();
        fixture.WorkValidator.ReceivedCalls().ShouldBeEmpty();
        fixture.PartyValidator.ReceivedCalls().ShouldBeEmpty();
        fixture.PolicyEvaluator.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Project_submission_validates_authority_and_dispatches_with_server_context()
    {
        Fixture fixture = AuthorizedProjectFixture();

        TimeEntrySubmissionCommandResult result = await fixture.CreateSubmissionService().SubmitAsync(
            Context(),
            SubmitCommand(TimeEntryId()),
            States(RecordedState(ProjectCommand())),
            FreshCatalog(ActivityTypeScope.Tenant),
            SubmittedAtUtc(),
            TestContext.Current.CancellationToken);

        TimeEntrySubmissionEntryResult entry = result.Entries.ShouldHaveSingleItem();
        entry.WasDispatched.ShouldBeTrue();
        TimeEntrySubmitted submitted = entry.DomainResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimeEntrySubmitted>();
        submitted.Submitter.ShouldBe(Context().Actor);
        submitted.Tenant.ShouldBe(Context().Tenant);
        submitted.ApprovalState.ShouldBe(TimeEntryApprovalState.Submitted);
        await fixture.ProjectValidator.Received(1)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Project(), Arg.Any<CancellationToken>());
        await fixture.PartyValidator.Received(1)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Contributor(), Arg.Any<CancellationToken>());
        await fixture.PolicyEvaluator.Received(1)
            .EvaluateAsync(
                Arg.Is<TimesheetsAuthorizationRequest>(request =>
                    request != null
                    && request.Project == Project()
                    && request.Contributor == Contributor()),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Project_submission_fails_closed_on_project_authority_before_contributor_policy_or_domain_dispatch()
    {
        Fixture fixture = AuthorizedFixture();
        fixture.ProjectValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<ProjectReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Denied(
                ReferenceValidationState.Stale,
                "Project authority cannot be resolved."));

        TimeEntrySubmissionCommandResult result = await fixture.CreateSubmissionService().SubmitAsync(
            Context(),
            SubmitCommand(TimeEntryId()),
            States(RecordedState(ProjectCommand())),
            FreshCatalog(ActivityTypeScope.Tenant),
            SubmittedAtUtc(),
            TestContext.Current.CancellationToken);

        TimeEntrySubmissionEntryResult entry = result.Entries.ShouldHaveSingleItem();
        entry.WasDispatched.ShouldBeFalse();
        entry.DomainResult.ShouldBeNull();
        entry.Authorization.DenialCategory.ShouldBe(TimesheetsDenialCategory.StaleProjection);
        await fixture.ProjectValidator.Received(1)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Project(), Arg.Any<CancellationToken>());
        fixture.PartyValidator.ReceivedCalls().ShouldBeEmpty();
        fixture.PolicyEvaluator.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Submission_fails_closed_on_contributor_authority_before_policy_or_domain_dispatch()
    {
        Fixture fixture = AuthorizedFixture();
        fixture.ProjectValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<ProjectReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());
        fixture.PartyValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<PartyReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Denied(
                ReferenceValidationState.Unavailable,
                "Contributor authority cannot be resolved."));

        TimeEntrySubmissionCommandResult result = await fixture.CreateSubmissionService().SubmitAsync(
            Context(),
            SubmitCommand(TimeEntryId()),
            States(RecordedState(ProjectCommand())),
            FreshCatalog(ActivityTypeScope.Tenant),
            SubmittedAtUtc(),
            TestContext.Current.CancellationToken);

        TimeEntrySubmissionEntryResult entry = result.Entries.ShouldHaveSingleItem();
        entry.WasDispatched.ShouldBeFalse();
        entry.DomainResult.ShouldBeNull();
        entry.Authorization.DenialCategory.ShouldBe(TimesheetsDenialCategory.UnavailableSiblingAuthority);
        await fixture.ProjectValidator.Received(1)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Project(), Arg.Any<CancellationToken>());
        await fixture.PartyValidator.Received(1)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Contributor(), Arg.Any<CancellationToken>());
        fixture.PolicyEvaluator.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Submission_policy_denial_fails_before_catalog_or_domain_dispatch()
    {
        Fixture fixture = AuthorizedProjectFixture();
        fixture.PolicyEvaluator
            .EvaluateAsync(Arg.Any<TimesheetsAuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(TimesheetsPolicyEvaluationResult.Denied(
                TimesheetsDenialCategory.CommentPolicyMissing,
                "Comment policy is not resolved."));

        TimeEntrySubmissionCommandResult result = await fixture.CreateSubmissionService().SubmitAsync(
            Context(),
            SubmitCommand(TimeEntryId()),
            States(RecordedState(ProjectCommand())),
            FreshCatalog(ActivityTypeScope.Tenant),
            SubmittedAtUtc(),
            TestContext.Current.CancellationToken);

        TimeEntrySubmissionEntryResult entry = result.Entries.ShouldHaveSingleItem();
        entry.WasDispatched.ShouldBeFalse();
        entry.DomainResult.ShouldBeNull();
        entry.Authorization.DenialCategory.ShouldBe(TimesheetsDenialCategory.CommentPolicyMissing);
    }

    [Fact]
    public async Task Submission_rejects_stale_activity_type_catalog_before_domain_dispatch()
    {
        Fixture fixture = AuthorizedProjectFixture();

        TimeEntrySubmissionCommandResult result = await fixture.CreateSubmissionService().SubmitAsync(
            Context(),
            SubmitCommand(TimeEntryId()),
            States(RecordedState(ProjectCommand())),
            new([], ProjectionFreshnessMetadata.Stale("42")),
            SubmittedAtUtc(),
            TestContext.Current.CancellationToken);

        TimeEntrySubmissionEntryResult entry = result.Entries.ShouldHaveSingleItem();
        entry.WasDispatched.ShouldBeFalse();
        TimesheetsRejection rejection = SubmissionRejection(entry);
        rejection.Code.ShouldBe(TimesheetsRejectionCode.ProjectionUnavailable);
        rejection.FieldErrors.ShouldContain(static error =>
            error.Field == "entries[time-entry-1].activityTypeCatalog" && error.Code == "not-fresh");
    }

    [Fact]
    public async Task Submission_rejects_inactive_activity_type_before_domain_dispatch()
    {
        Fixture fixture = AuthorizedProjectFixture();

        TimeEntrySubmissionCommandResult result = await fixture.CreateSubmissionService().SubmitAsync(
            Context(),
            SubmitCommand(TimeEntryId()),
            States(RecordedState(ProjectCommand())),
            Catalog(Item(ActivityId(), ActivityTypeScope.Tenant, null, false)),
            SubmittedAtUtc(),
            TestContext.Current.CancellationToken);

        TimeEntrySubmissionEntryResult entry = result.Entries.ShouldHaveSingleItem();
        entry.WasDispatched.ShouldBeFalse();
        TimesheetsRejection rejection = SubmissionRejection(entry);
        rejection.Code.ShouldBe(TimesheetsRejectionCode.ActivityTypeInactive);
        rejection.FieldErrors.ShouldContain(static error =>
            error.Field == "entries[time-entry-1].activityTypeId" && error.Code == "unavailable");
    }

    [Fact]
    public async Task Work_submission_rejects_project_activity_type_until_governing_project_adapter_exists()
    {
        Fixture fixture = AuthorizedFixture();
        fixture.WorkValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<WorkReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());
        fixture.PartyValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<PartyReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());

        TimeEntrySubmissionCommandResult result = await fixture.CreateSubmissionService().SubmitAsync(
            Context(),
            SubmitCommand(TimeEntryId()),
            States(RecordedState(WorkCommand())),
            Catalog(Item(ActivityId(), ActivityTypeScope.Project, Project(), true)),
            SubmittedAtUtc(),
            TestContext.Current.CancellationToken);

        TimeEntrySubmissionEntryResult entry = result.Entries.ShouldHaveSingleItem();
        entry.WasDispatched.ShouldBeFalse();
        TimesheetsRejection rejection = SubmissionRejection(entry);
        rejection.Code.ShouldBe(TimesheetsRejectionCode.AuthorityCannotBeResolved);
        rejection.FieldErrors.ShouldContain(static error =>
            error.Field == "entries[time-entry-1].target" && error.Code == "work-project-unresolved");
    }

    [Fact]
    public async Task Submission_partial_batch_reports_accepted_and_blocked_entries()
    {
        Fixture fixture = AuthorizedProjectFixture();
        TimeEntryId validId = TimeEntryId();
        TimeEntryId invalidId = new("time-entry-2");

        TimeEntrySubmissionCommandResult result = await fixture.CreateSubmissionService().SubmitAsync(
            Context(),
            SubmitCommand(validId, invalidId),
            new Dictionary<TimeEntryId, TimeEntryState?>
            {
                [validId] = RecordedState(ProjectCommand()),
                [invalidId] = RecordedState(ProjectCommand() with
                {
                    TimeEntryId = invalidId,
                    ActivityTypeId = new ActivityTypeId("missing-activity-type")
                })
            },
            FreshCatalog(ActivityTypeScope.Tenant),
            SubmittedAtUtc(),
            TestContext.Current.CancellationToken);

        result.Entries.Count.ShouldBe(2);
        result.HasAcceptedEvents.ShouldBeTrue();
        result.HasBlockedEntries.ShouldBeTrue();
        result.IsPartial.ShouldBeTrue();
        result.Entries.Single(entry => entry.TimeEntryId == validId)
            .DomainResult.ShouldNotBeNull()
            .IsSuccess.ShouldBeTrue();
        SubmissionRejection(result.Entries.Single(entry => entry.TimeEntryId == invalidId))
            .Code.ShouldBe(TimesheetsRejectionCode.ActivityTypeNotFound);
    }

    [Fact]
    public async Task Submission_deduplicates_repeated_time_entry_ids_into_a_single_transition()
    {
        Fixture fixture = AuthorizedProjectFixture();
        TimeEntryId duplicatedId = TimeEntryId();

        TimeEntrySubmissionCommandResult result = await fixture.CreateSubmissionService().SubmitAsync(
            Context(),
            SubmitCommand(duplicatedId, duplicatedId),
            States(RecordedState(ProjectCommand())),
            FreshCatalog(ActivityTypeScope.Tenant),
            SubmittedAtUtc(),
            TestContext.Current.CancellationToken);

        TimeEntrySubmissionEntryResult entry = result.Entries.ShouldHaveSingleItem();
        entry.TimeEntryId.ShouldBe(duplicatedId);
        entry.WasDispatched.ShouldBeTrue();
        entry.DomainResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimeEntrySubmitted>();
        result.HasAcceptedEvents.ShouldBeTrue();
        result.HasBlockedEntries.ShouldBeFalse();
    }

    [Fact]
    public async Task Approval_fails_closed_on_base_authority_before_resolver_or_domain_dispatch()
    {
        Fixture fixture = new();
        fixture.TenantValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), TimesheetsOperation.Command, Arg.Any<CancellationToken>())
            .Returns(TimesheetsTenantAccessResult.Denied(
                TimesheetsTenantAccessState.MissingTenant,
                "tenant-1 project-1 raw upstream detail"));

        TimeEntryApprovalCommandResult result = await fixture.CreateApprovalService().ApproveAsync(
            Context(),
            ApproveCommand(TimeEntryId()),
            SubmittedState(ProjectCommand()),
            DecisionAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeFalse();
        result.DomainResult.ShouldBeNull();
        result.AuthorityResolution.ShouldBeNull();
        result.Authorization.DenialCategory.ShouldBe(TimesheetsDenialCategory.MissingTenant);
        result.Authorization.Reason.ShouldBe("Authority cannot be resolved.");
        fixture.ApprovalResolver.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Project_approval_uses_same_contributor_source_for_access_and_resolver_then_dispatches()
    {
        Fixture fixture = AuthorizedProjectFixture();
        fixture.ApprovalResolver
            .ResolveAsync(Arg.Any<ApprovalAuthorityResolutionRequest>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(AllowedResolution(ApprovalAuthorityAction.EntryApproval)));

        TimeEntryApprovalCommandResult result = await fixture.CreateApprovalService().ApproveAsync(
            Context(),
            ApproveCommand(TimeEntryId()),
            SubmittedState(ProjectCommand()),
            DecisionAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeTrue();
        TimeEntryApproved approved = result.DomainResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimeEntryApproved>();
        approved.Approver.ShouldBe(Context().Actor);
        approved.Tenant.ShouldBe(Context().Tenant);
        approved.AuthoritySource.Source.ShouldBe(ApprovalAuthoritySource.ProjectApprover);
        await fixture.ProjectValidator.Received(1)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Project(), Arg.Any<CancellationToken>());
        await fixture.PartyValidator.Received(1)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Contributor(), Arg.Any<CancellationToken>());
        await fixture.ApprovalResolver.Received(1)
            .ResolveAsync(
                Arg.Is<ApprovalAuthorityResolutionRequest>(request =>
                    request != null
                    && request.Action == ApprovalAuthorityAction.EntryApproval
                    && request.Contributor == Contributor()
                    && request.AuthorizationRequest.Contributor == Contributor()
                    && request.AuthorizationRequest.Project == Project()),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Rejection_uses_entry_rejection_authority_action_and_dispatches_rejected_event()
    {
        Fixture fixture = AuthorizedProjectFixture();
        fixture.ApprovalResolver
            .ResolveAsync(Arg.Any<ApprovalAuthorityResolutionRequest>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(AllowedResolution(ApprovalAuthorityAction.EntryRejection)));

        TimeEntryApprovalCommandResult result = await fixture.CreateApprovalService().RejectAsync(
            Context(),
            RejectCommand(TimeEntryId()),
            SubmittedState(ProjectCommand()),
            DecisionAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeTrue();
        TimeEntryRejected rejected = result.DomainResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimeEntryRejected>();
        rejected.Reason.Value.ShouldBe("Needs customer PO evidence.");
        rejected.AuthoritySource.Action.ShouldBe(ApprovalAuthorityAction.EntryRejection);
        await fixture.ApprovalResolver.Received(1)
            .ResolveAsync(
                Arg.Is<ApprovalAuthorityResolutionRequest>(request =>
                    request != null
                    && request.Action == ApprovalAuthorityAction.EntryRejection
                    && request.Contributor == Contributor()
                    && request.AuthorizationRequest.Contributor == Contributor()),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Approval_authority_denial_uses_safe_copy_and_does_not_dispatch_domain()
    {
        Fixture fixture = AuthorizedProjectFixture();
        fixture.ApprovalResolver
            .ResolveAsync(Arg.Any<ApprovalAuthorityResolutionRequest>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(ApprovalAuthorityResolutionResult.Denied(
                TimesheetsDenialCategory.AmbiguousAuthority,
                "tenant-1 project-1 role Owner upstream detail",
                new ApprovalAuthoritySourceAttribution(
                    ApprovalAuthorityAction.EntryApproval,
                    ApprovalAuthoritySource.ProjectApprover,
                    ApprovalAuthorityDecisionState.Ambiguous,
                    "timesheets.approval-authority.v1",
                    "v1",
                    ProjectionFreshnessMetadata.Stale()))));

        TimeEntryApprovalCommandResult result = await fixture.CreateApprovalService().ApproveAsync(
            Context(),
            ApproveCommand(TimeEntryId()),
            SubmittedState(ProjectCommand()),
            DecisionAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeFalse();
        result.DomainResult.ShouldBeNull();
        result.AuthorityResolution.ShouldNotBeNull();
        result.AuthorityResolution.IsAllowed.ShouldBeFalse();
        result.AuthorityResolution.Reason.ShouldBe("Authority cannot be resolved.");
        result.AuthorityResolution.Reason.ShouldNotContain("project", Case.Insensitive);
        result.AuthorityResolution.Reason.ShouldNotContain("role", Case.Insensitive);
    }

    [Fact]
    public async Task Approval_service_denies_self_approval_by_default_through_resolver()
    {
        Fixture fixture = AuthorizedFixture();
        fixture.ProjectValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<ProjectReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());
        fixture.PartyValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<PartyReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());
        TimeEntryApprovalCommandService service = fixture.CreateApprovalService(
            new TimesheetsApprovalAuthorityResolver(
                TimesheetsApprovalAuthorityPolicyOptions.Default,
                [new AllowingAuthorityProvider(ApprovalAuthoritySource.ProjectApprover)]));

        TimeEntryApprovalCommandResult result = await service.ApproveAsync(
            Context(),
            ApproveCommand(TimeEntryId()),
            SubmittedState(ProjectCommand() with { Contributor = Context().Actor! }),
            DecisionAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeFalse();
        result.AuthorityResolution.ShouldNotBeNull().DenialCategory.ShouldBe(TimesheetsDenialCategory.InsufficientRole);
        result.AuthorityResolution.Reason.ShouldBe("Access denied for this action.");
    }

    [Fact]
    public async Task Approval_service_allows_self_approval_only_when_policy_explicitly_allows_entry_approval()
    {
        Fixture fixture = AuthorizedFixture();
        fixture.ProjectValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<ProjectReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());
        fixture.PartyValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<PartyReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());
        TimeEntryApprovalCommandService service = fixture.CreateApprovalService(
            new TimesheetsApprovalAuthorityResolver(
                new TimesheetsApprovalAuthorityPolicyOptions
                {
                    SelfApprovalAllowedActions = new HashSet<ApprovalAuthorityAction>
                    {
                        ApprovalAuthorityAction.EntryApproval
                    }
                },
                []));

        TimeEntryApprovalCommandResult result = await service.ApproveAsync(
            Context(),
            ApproveCommand(TimeEntryId()),
            SubmittedState(ProjectCommand() with { Contributor = Context().Actor! }),
            DecisionAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeTrue();
        result.DomainResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimeEntryApproved>()
            .AuthoritySource.Source.ShouldBe(ApprovalAuthoritySource.SelfApprovalPolicy);
    }

    [Fact]
    public async Task Correction_fails_closed_on_current_entry_authority_before_corrected_reference_or_domain_dispatch()
    {
        Fixture fixture = AuthorizedFixture();
        fixture.ProjectValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<ProjectReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Denied(
                ReferenceValidationState.TenantMismatch,
                "Project authority cannot be resolved."));

        TimeEntryCorrectionCommandResult result = await fixture.CreateCorrectionService().CorrectAsync(
            Context(),
            CorrectCommand(TimeEntryId()),
            RejectedState(ProjectCommand()),
            FreshCatalog(ActivityTypeScope.Tenant),
            CorrectionAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeFalse();
        result.DomainResult.ShouldBeNull();
        result.HasCurrentAuthorizationDenial.ShouldBeTrue();
        result.CurrentAuthorization.DenialCategory.ShouldBe(TimesheetsDenialCategory.CrossTenantTarget);
        result.CurrentAuthorization.Reason.ShouldBe("Authority cannot be resolved.");
        result.CorrectedAuthorization.ShouldBeNull();
        await fixture.ProjectValidator.Received(1)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Project(), Arg.Any<CancellationToken>());
        await fixture.PartyValidator.Received(0)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<PartyReference>(), Arg.Any<CancellationToken>());
        fixture.ApprovalResolver.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Approved_entry_correction_attempt_fails_closed_on_current_authority_before_lock_guard_dispatch()
    {
        Fixture fixture = AuthorizedFixture();
        fixture.ProjectValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<ProjectReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Denied(
                ReferenceValidationState.TenantMismatch,
                "tenant-1 project-1 raw upstream detail"));

        TimeEntryCorrectionCommandResult result = await fixture.CreateCorrectionService().CorrectAsync(
            Context(),
            CorrectCommand(TimeEntryId()),
            ApprovedState(ProjectCommand()),
            FreshCatalog(ActivityTypeScope.Tenant),
            CorrectionAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeFalse();
        result.DomainResult.ShouldBeNull();
        result.HasCurrentAuthorizationDenial.ShouldBeTrue();
        result.CurrentAuthorization.DenialCategory.ShouldBe(TimesheetsDenialCategory.CrossTenantTarget);
        result.CurrentAuthorization.Reason.ShouldBe("Authority cannot be resolved.");
        result.CurrentAuthorization.Reason.ShouldNotContain("project", Case.Insensitive);
        result.CorrectedAuthorization.ShouldBeNull();
        await fixture.ProjectValidator.Received(1)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Project(), Arg.Any<CancellationToken>());
        fixture.ApprovalResolver.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Correction_validates_current_and_corrected_references_then_uses_correction_authority_action()
    {
        Fixture fixture = AuthorizedProjectFixture();
        fixture.ApprovalResolver
            .ResolveAsync(Arg.Any<ApprovalAuthorityResolutionRequest>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(AllowedResolution(ApprovalAuthorityAction.CorrectionAuthorization)));

        TimeEntryCorrectionCommandResult result = await fixture.CreateCorrectionService().CorrectAsync(
            Context(),
            CorrectCommand(TimeEntryId()),
            RejectedState(ProjectCommand()),
            FreshCatalog(ActivityTypeScope.Tenant),
            CorrectionAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeTrue();
        TimeEntryCorrected corrected = result.DomainResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimeEntryCorrected>();
        corrected.CorrectedBy.ShouldBe(Context().Actor);
        corrected.Tenant.ShouldBe(Context().Tenant);
        corrected.CorrectionState.ShouldBe(TimeEntryCorrectionState.Corrected);
        await fixture.ProjectValidator.Received(2)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Project(), Arg.Any<CancellationToken>());
        await fixture.PartyValidator.Received(2)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Contributor(), Arg.Any<CancellationToken>());
        await fixture.ApprovalResolver.Received(1)
            .ResolveAsync(
                Arg.Is<ApprovalAuthorityResolutionRequest>(request =>
                    request != null
                    && request.Action == ApprovalAuthorityAction.CorrectionAuthorization
                    && request.Contributor == Contributor()
                    && request.AuthorizationRequest.Contributor == Contributor()
                    && request.AuthorizationRequest.Project == Project()),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Approved_correction_validates_current_and_corrected_references_then_dispatches_additive_event()
    {
        Fixture fixture = AuthorizedProjectFixture();
        fixture.ApprovalResolver
            .ResolveAsync(Arg.Any<ApprovalAuthorityResolutionRequest>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(AllowedResolution(ApprovalAuthorityAction.CorrectionAuthorization)));

        TimeEntryCorrectionCommandResult result = await fixture.CreateCorrectionService().CorrectAsync(
            Context(),
            ApprovedCorrectionCommand(TimeEntryId()),
            ApprovedState(ProjectCommand()),
            FreshCatalog(ActivityTypeScope.Tenant),
            CorrectionAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeTrue();
        TimeEntryApprovedCorrected corrected = result.DomainResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimeEntryApprovedCorrected>();
        corrected.CorrectedBy.ShouldBe(Context().Actor);
        corrected.Tenant.ShouldBe(Context().Tenant);
        corrected.ApprovalState.ShouldBe(TimeEntryApprovalState.Approved);
        corrected.CorrectionState.ShouldBe(TimeEntryCorrectionState.Corrected);
        await fixture.ProjectValidator.Received(2)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Project(), Arg.Any<CancellationToken>());
        await fixture.PartyValidator.Received(2)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Contributor(), Arg.Any<CancellationToken>());
        await fixture.ApprovalResolver.Received(1)
            .ResolveAsync(
                Arg.Is<ApprovalAuthorityResolutionRequest>(request =>
                    request != null
                    && request.Action == ApprovalAuthorityAction.CorrectionAuthorization
                    && request.Contributor == Contributor()
                    && request.AuthorizationRequest.Contributor == Contributor()
                    && request.AuthorizationRequest.Project == Project()),
                Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Correction_fails_closed_on_corrected_target_authority_before_resolver_or_domain_dispatch()
    {
        Fixture fixture = AuthorizedFixture();
        ProjectReference correctedProject = new("project-2");
        fixture.ProjectValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Project(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());
        fixture.ProjectValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), correctedProject, Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Denied(
                ReferenceValidationState.TenantMismatch,
                "tenant-1 project-2 raw upstream detail"));
        fixture.PartyValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<PartyReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());

        TimeEntryCorrectionCommandResult result = await fixture.CreateCorrectionService().CorrectAsync(
            Context(),
            CorrectCommand(TimeEntryId()) with { Target = TimeEntryTargetReference.ForProject(correctedProject) },
            RejectedState(ProjectCommand()),
            Catalog(Item(ActivityId(), ActivityTypeScope.Project, correctedProject, true)),
            CorrectionAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeFalse();
        result.HasCorrectedAuthorizationDenial.ShouldBeTrue();
        result.CorrectedAuthorization.ShouldNotBeNull().DenialCategory.ShouldBe(TimesheetsDenialCategory.CrossTenantTarget);
        result.CorrectedAuthorization.ShouldNotBeNull().Reason.ShouldBe("Authority cannot be resolved.");
        result.AuthorityResolution.ShouldBeNull();
        result.DomainResult.ShouldBeNull();
        fixture.ApprovalResolver.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Correction_authority_denial_uses_safe_copy_and_does_not_dispatch_domain()
    {
        Fixture fixture = AuthorizedProjectFixture();
        fixture.ApprovalResolver
            .ResolveAsync(Arg.Any<ApprovalAuthorityResolutionRequest>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(ApprovalAuthorityResolutionResult.Denied(
                TimesheetsDenialCategory.AmbiguousAuthority,
                "tenant-1 project-1 role Owner upstream detail",
                new ApprovalAuthoritySourceAttribution(
                    ApprovalAuthorityAction.CorrectionAuthorization,
                    ApprovalAuthoritySource.ProjectApprover,
                    ApprovalAuthorityDecisionState.Ambiguous,
                    "timesheets.approval-authority.v1",
                    "v1",
                    ProjectionFreshnessMetadata.Stale()))));

        TimeEntryCorrectionCommandResult result = await fixture.CreateCorrectionService().CorrectAsync(
            Context(),
            CorrectCommand(TimeEntryId()),
            RejectedState(ProjectCommand()),
            FreshCatalog(ActivityTypeScope.Tenant),
            CorrectionAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeFalse();
        result.HasCorrectionPolicyDenial.ShouldBeTrue();
        result.DomainResult.ShouldBeNull();
        result.AuthorityResolution.ShouldNotBeNull();
        result.AuthorityResolution.Reason.ShouldBe("Authority cannot be resolved.");
        result.AuthorityResolution.Reason.ShouldNotContain("project", Case.Insensitive);
        result.AuthorityResolution.Reason.ShouldNotContain("role", Case.Insensitive);
    }

    [Fact]
    public async Task Correction_rejects_stale_activity_type_catalog_before_domain_dispatch()
    {
        Fixture fixture = AuthorizedProjectFixture();
        fixture.ApprovalResolver
            .ResolveAsync(Arg.Any<ApprovalAuthorityResolutionRequest>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(AllowedResolution(ApprovalAuthorityAction.CorrectionAuthorization)));

        TimeEntryCorrectionCommandResult result = await fixture.CreateCorrectionService().CorrectAsync(
            Context(),
            CorrectCommand(TimeEntryId()),
            RejectedState(ProjectCommand()),
            new([], ProjectionFreshnessMetadata.Stale("42")),
            CorrectionAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeFalse();
        TimesheetsRejection rejection = CorrectionRejection(result);
        rejection.Code.ShouldBe(TimesheetsRejectionCode.ProjectionUnavailable);
        rejection.FieldErrors.ShouldContain(static error =>
            error.Field == "activityTypeCatalog" && error.Code == "not-fresh");
    }

    [Fact]
    public async Task Correction_authority_is_provider_resolved_and_fails_closed_for_self_correction_without_a_granting_provider()
    {
        // Documents the chosen correction-policy boundary: correction is resolved through the
        // approval-authority providers (CorrectionAuthorization), NOT contributor-self-owned. The
        // resolver's self-approval shortcut applies only to entry/period approval, so a contributor
        // correcting their own rejected entry must still obtain provider authority and fails closed
        // when no provider grants it. Uses the real resolver with no source providers configured.
        Fixture fixture = AuthorizedProjectFixture();
        TimeEntryCorrectionCommandService service = fixture.CreateCorrectionService(
            new TimesheetsApprovalAuthorityResolver(
                TimesheetsApprovalAuthorityPolicyOptions.Default,
                []));

        TimeEntryCorrectionCommandResult result = await service.CorrectAsync(
            Context(),
            CorrectCommand(TimeEntryId()) with { Contributor = Context().Actor! },
            RejectedState(ProjectCommand() with { Contributor = Context().Actor! }),
            FreshCatalog(ActivityTypeScope.Tenant),
            CorrectionAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeFalse();
        result.HasCorrectionPolicyDenial.ShouldBeTrue();
        result.DomainResult.ShouldBeNull();
        result.AuthorityResolution.ShouldNotBeNull().IsAllowed.ShouldBeFalse();
        result.AuthorityResolution.DenialCategory.ShouldBe(TimesheetsDenialCategory.UnavailableSiblingAuthority);
        result.AuthorityResolution.SourceAttribution.Action.ShouldBe(ApprovalAuthorityAction.CorrectionAuthorization);
        result.AuthorityResolution.Reason.ShouldBe("Authority cannot be resolved.");
    }

    [Fact]
    public async Task Evidence_query_fails_closed_before_projection_lookup_when_tenant_authority_is_missing()
    {
        Fixture fixture = new();
        fixture.TenantValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), TimesheetsOperation.ProjectionRead, Arg.Any<CancellationToken>())
            .Returns(TimesheetsTenantAccessResult.Denied(
                TimesheetsTenantAccessState.MissingTenant,
                "Authority cannot be resolved."));

        TimeEntryEvidenceQueryResult result = await fixture.CreateQueryService().ReadAsync(
            Context(),
            TimeEntryId(),
            TestContext.Current.CancellationToken);

        result.WasDisclosed.ShouldBeFalse();
        result.Evidence.ShouldBeNull();
        result.Outcome.ShouldBe(TimeEntryEvidenceQueryOutcome.NotFoundOrDenied);
        result.Authorization.DenialCategory.ShouldBe(TimesheetsDenialCategory.MissingTenant);
        fixture.ProjectionReader.ReceivedCalls().ShouldBeEmpty();
        fixture.DisplayHydrator.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Evidence_query_returns_non_disclosing_not_found_when_projection_has_no_entry()
    {
        Fixture fixture = AuthorizedProjectionReadFixture();
        fixture.ProjectionReader
            .ReadAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<TimeEntryId>(), Arg.Any<CancellationToken>())
            .Returns((TimeEntryEvidenceReadModel?)null);

        TimeEntryEvidenceQueryResult result = await fixture.CreateQueryService().ReadAsync(
            Context(),
            TimeEntryId(),
            TestContext.Current.CancellationToken);

        result.WasDisclosed.ShouldBeFalse();
        result.Evidence.ShouldBeNull();
        result.Outcome.ShouldBe(TimeEntryEvidenceQueryOutcome.NotFoundOrDenied);
        result.Authorization.IsAuthorized.ShouldBeTrue();
        await fixture.ProjectionReader.Received(1)
            .ReadAsync(Arg.Any<TimesheetsRequestContext>(), TimeEntryId(), Arg.Any<CancellationToken>());
        fixture.DisplayHydrator.ReceivedCalls().ShouldBeEmpty();
    }

    [Theory]
    [InlineData(ReferenceValidationState.TenantMismatch, TimesheetsDenialCategory.CrossTenantTarget)]
    [InlineData(ReferenceValidationState.Stale, TimesheetsDenialCategory.StaleProjection)]
    [InlineData(ReferenceValidationState.Unavailable, TimesheetsDenialCategory.UnavailableSiblingAuthority)]
    [InlineData(ReferenceValidationState.Ambiguous, TimesheetsDenialCategory.AmbiguousAuthority)]
    [InlineData(ReferenceValidationState.InvalidReference, TimesheetsDenialCategory.InvalidReference)]
    public async Task Evidence_query_fails_closed_on_project_authority_before_returning_identifiers_lineage_or_labels(
        ReferenceValidationState referenceState,
        TimesheetsDenialCategory expectedCategory)
    {
        Fixture fixture = AuthorizedProjectionReadFixture();
        fixture.ProjectionReader
            .ReadAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<TimeEntryId>(), Arg.Any<CancellationToken>())
            .Returns(EvidenceModel());
        fixture.ProjectValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<ProjectReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Denied(referenceState, "Authority cannot be resolved."));

        TimeEntryEvidenceQueryResult result = await fixture.CreateQueryService().ReadAsync(
            Context(),
            TimeEntryId(),
            TestContext.Current.CancellationToken);

        result.WasDisclosed.ShouldBeFalse();
        result.Evidence.ShouldBeNull();
        result.Authorization.DenialCategory.ShouldBe(expectedCategory);
        await fixture.ProjectValidator.Received(1)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Project(), Arg.Any<CancellationToken>());
        fixture.DisplayHydrator.ReceivedCalls().ShouldBeEmpty();
    }

    [Theory]
    [InlineData(ReferenceValidationState.Unauthorized, TimesheetsDenialCategory.InsufficientRole)]
    [InlineData(ReferenceValidationState.TenantMismatch, TimesheetsDenialCategory.CrossTenantTarget)]
    [InlineData(ReferenceValidationState.Unavailable, TimesheetsDenialCategory.UnavailableSiblingAuthority)]
    public async Task Evidence_query_fails_closed_on_contributor_authority_before_returning_lineage_or_labels(
        ReferenceValidationState referenceState,
        TimesheetsDenialCategory expectedCategory)
    {
        Fixture fixture = AuthorizedProjectionReadFixture();
        fixture.ProjectionReader
            .ReadAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<TimeEntryId>(), Arg.Any<CancellationToken>())
            .Returns(EvidenceModel());
        fixture.ProjectValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<ProjectReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());
        fixture.PartyValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<PartyReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Denied(referenceState, "Contributor authority cannot be resolved."));

        TimeEntryEvidenceQueryResult result = await fixture.CreateQueryService().ReadAsync(
            Context(),
            TimeEntryId(),
            TestContext.Current.CancellationToken);

        result.WasDisclosed.ShouldBeFalse();
        result.Evidence.ShouldBeNull();
        result.Authorization.DenialCategory.ShouldBe(expectedCategory);
        await fixture.ProjectValidator.Received(1)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Project(), Arg.Any<CancellationToken>());
        await fixture.PartyValidator.Received(1)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Contributor(), Arg.Any<CancellationToken>());
        fixture.DisplayHydrator.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Evidence_query_policy_denial_after_reference_validation_returns_no_evidence()
    {
        Fixture fixture = AuthorizedProjectionReadFixture();
        fixture.ProjectionReader
            .ReadAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<TimeEntryId>(), Arg.Any<CancellationToken>())
            .Returns(EvidenceModel());
        fixture.ProjectValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<ProjectReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());
        fixture.PartyValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<PartyReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());
        fixture.PolicyEvaluator
            .EvaluateAsync(Arg.Is<TimesheetsAuthorizationRequest>(request =>
                request != null
                && request.Operation == TimesheetsOperation.ProjectionRead
                && request.Project == Project()
                && request.Contributor == Contributor()),
                Arg.Any<CancellationToken>())
            .Returns(TimesheetsPolicyEvaluationResult.Denied(
                TimesheetsDenialCategory.RetentionPolicyMissing,
                "Evidence policy is not configured."));

        TimeEntryEvidenceQueryResult result = await fixture.CreateQueryService().ReadAsync(
            Context(),
            TimeEntryId(),
            TestContext.Current.CancellationToken);

        result.WasDisclosed.ShouldBeFalse();
        result.Evidence.ShouldBeNull();
        result.Authorization.DenialCategory.ShouldBe(TimesheetsDenialCategory.RetentionPolicyMissing);
        fixture.DisplayHydrator.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Evidence_query_validates_work_target_without_project_lookup()
    {
        Fixture fixture = AuthorizedProjectionReadFixture();
        fixture.ProjectionReader
            .ReadAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<TimeEntryId>(), Arg.Any<CancellationToken>())
            .Returns(EvidenceModel(TimeEntryTargetReference.ForWork(Work())));
        fixture.WorkValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<WorkReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());
        fixture.PartyValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<PartyReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());
        fixture.DisplayHydrator
            .HydrateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<TimeEntryEvidenceReadModel>(), Arg.Any<CancellationToken>())
            .Returns(new TimeEntryDisplayHydration(
                TimeEntryHydratedDisplayLabel.Fresh("Contributor"),
                TimeEntryHydratedDisplayLabel.Fresh("Work"),
                TimeEntryHydratedDisplayLabel.Unavailable()));

        TimeEntryEvidenceQueryResult result = await fixture.CreateQueryService().ReadAsync(
            Context(),
            TimeEntryId(),
            TestContext.Current.CancellationToken);

        result.WasDisclosed.ShouldBeTrue();
        result.Evidence.ShouldNotBeNull();
        result.Evidence.Target.TargetKind.ShouldBe(TimeEntryTargetKind.Work);
        result.Evidence.DisplayHydration.Target.Label.ShouldBe("Work");
        fixture.ProjectValidator.ReceivedCalls().ShouldBeEmpty();
        await fixture.WorkValidator.Received(1)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Work(), Arg.Any<CancellationToken>());
        await fixture.PartyValidator.Received(1)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Contributor(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Evidence_query_validates_project_and_contributor_then_returns_read_time_hydrated_evidence()
    {
        Fixture fixture = AuthorizedProjectionReadFixture();
        fixture.ProjectionReader
            .ReadAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<TimeEntryId>(), Arg.Any<CancellationToken>())
            .Returns(EvidenceModel());
        fixture.ProjectValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<ProjectReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());
        fixture.PartyValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<PartyReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());
        fixture.DisplayHydrator
            .HydrateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<TimeEntryEvidenceReadModel>(), Arg.Any<CancellationToken>())
            .Returns(new TimeEntryDisplayHydration(
                TimeEntryHydratedDisplayLabel.Fresh("Contributor"),
                TimeEntryHydratedDisplayLabel.Fresh("Project"),
                TimeEntryHydratedDisplayLabel.Unavailable()));

        TimeEntryEvidenceQueryResult result = await fixture.CreateQueryService().ReadAsync(
            Context(),
            TimeEntryId(),
            TestContext.Current.CancellationToken);

        result.WasDisclosed.ShouldBeTrue();
        result.Evidence.ShouldNotBeNull();
        result.Evidence.Target.TargetId.ShouldBe(Project().ProjectId);
        result.Evidence.DisplayHydration.Target.Label.ShouldBe("Project");
        await fixture.ProjectValidator.Received(1)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Project(), Arg.Any<CancellationToken>());
        await fixture.PartyValidator.Received(1)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Contributor(), Arg.Any<CancellationToken>());
        await fixture.DisplayHydrator.Received(1)
            .HydrateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<TimeEntryEvidenceReadModel>(), Arg.Any<CancellationToken>());
    }

    private static ActivityTypeCatalogReadModel FreshCatalog(ActivityTypeScope scope)
        => new(
            [
                new(
                    ActivityId(),
                    scope,
                    scope == ActivityTypeScope.Project ? Project() : null,
                    "Delivery",
                    true,
                    BillableState.Billable)
            ],
            ProjectionFreshnessMetadata.Fresh);

    private static ActivityTypeCatalogReadModel Catalog(params ActivityTypeCatalogItem[] items)
        => new(items, ProjectionFreshnessMetadata.Fresh);

    private static ActivityTypeCatalogItem Item(
        ActivityTypeId activityTypeId,
        ActivityTypeScope scope,
        ProjectReference? project,
        bool isActive)
        => new(
            activityTypeId,
            scope,
            project,
            "Delivery",
            isActive,
            BillableState.Billable);

    private static TimesheetsRequestContext Context()
        => new(
            new TenantReference("tenant-1"),
            new PartyReference("operator-1"),
            "correlation-1");

    private static RecordTimeEntry ProjectCommand()
        => Command(TimeEntryTargetReference.ForProject(Project()));

    private static RecordTimeEntry AiProjectCommand()
        => Command(TimeEntryTargetReference.ForProject(Project())) with
        {
            ContributorCategory = ContributorCategory.AutomatedAgent,
            AiMetrics = new(
                AiMetricAvailability.ProviderReported,
                90000,
                75000,
                2,
                1000,
                250,
                1250,
                AiEffortMetricSourceMetadata.Provider("generic-provider", "capture-tool", "work-execution-1"),
                AiTokenMetricAvailability.ProviderReported)
        };

    private static RecordTimeEntry WorkCommand()
        => Command(TimeEntryTargetReference.ForWork(Work()));

    private static SubmitTimeEntriesForApproval SubmitCommand(params TimeEntryId[] timeEntryIds)
        => new(
            new TimeEntrySubmissionId("submission-1"),
            timeEntryIds,
            TimeEntrySubmissionScope.SelectedEntries);

    private static ApproveTimeEntry ApproveCommand(TimeEntryId timeEntryId)
        => new(
            timeEntryId,
            new TimeEntryApprovalDecisionId("decision-1"));

    private static RejectTimeEntry RejectCommand(TimeEntryId timeEntryId)
        => new(
            timeEntryId,
            new TimeEntryApprovalDecisionId("decision-1"),
            new TimeEntryRejectionReason("Needs customer PO evidence."));

    private static CorrectRejectedTimeEntry CorrectCommand(TimeEntryId timeEntryId)
        => new(
            timeEntryId,
            new TimeEntryCorrectionId("correction-1"),
            TimeEntryTargetReference.ForProject(Project()),
            Contributor(),
            ActivityId(),
            new DateOnly(2026, 6, 20),
            75,
            BillableState.Billable,
            ContributorCategory.Employee,
            AiEffortMetrics.Unavailable);

    private static CorrectApprovedTimeEntry ApprovedCorrectionCommand(TimeEntryId timeEntryId)
        => new(
            timeEntryId,
            new TimeEntryCorrectionId("approved-correction-1"),
            TimeEntryTargetReference.ForProject(Project()),
            Contributor(),
            ActivityId(),
            new DateOnly(2026, 6, 20),
            75,
            BillableState.Billable,
            ContributorCategory.Employee,
            AiEffortMetrics.Unavailable,
            new TimeEntryCorrectionReason("Correct approved duration after audit review."));

    private static RecordTimeEntry Command(TimeEntryTargetReference target)
        => new(
            TimeEntryId(),
            target,
            Contributor(),
            ActivityId(),
            new DateOnly(2026, 6, 19),
            60,
            BillableState.Billable,
            ContributorCategory.Employee,
            null);

    private static TimeEntryEvidenceReadModel EvidenceModel()
        => EvidenceModel(TimeEntryTargetReference.ForProject(Project()));

    private static TimeEntryEvidenceReadModel EvidenceModel(TimeEntryTargetReference target)
        => new(
            TimeEntryId(),
            target,
            Contributor(),
            ActivityId(),
            ActivityTypeScope.Tenant,
            new DateOnly(2026, 6, 19),
            60,
            BillableState.Billable,
            TimeEntryApprovalState.Draft,
            ContributorCategory.Employee,
            AiEffortMetrics.Unavailable,
            TimeEntryCorrectionState.None,
            ProjectionFreshnessMetadata.Fresh)
        {
            SourceAuthority = TimeEntryEvidenceSourceAuthority.TimesheetsDomainEvents,
            EventLineage =
            [
                new(nameof(TimeEntryRecorded), 1, TimeEntryEvidenceSourceAuthority.TimesheetsDomainEvents)
            ]
        };

    private static IReadOnlyDictionary<TimeEntryId, TimeEntryState?> States(TimeEntryState state)
        => new Dictionary<TimeEntryId, TimeEntryState?> { [state.TimeEntryId.ShouldNotBeNull()] = state };

    private static TimeEntryState RecordedState(RecordTimeEntry command)
    {
        TimeEntryState state = new();
        state.Apply(new TimeEntryRecorded(
            command.TimeEntryId,
            command.Target,
            command.Contributor,
            command.ActivityTypeId,
            ActivityTypeScope.Tenant,
            command.ServiceDate,
            command.DurationMinutes,
            command.BillableState,
            TimeEntryApprovalState.Draft,
            command.ContributorCategory,
            command.AiMetrics)
        {
            Comment = command.Comment
        });
        return state;
    }

    private static TimeEntryState SubmittedState(RecordTimeEntry command)
    {
        TimeEntryState state = RecordedState(command);
        state.Apply(new TimeEntrySubmitted(
            command.TimeEntryId,
            new PartyReference("submitter-1"),
            new TenantReference("tenant-1"),
            SubmittedAtUtc(),
            new TimeEntrySubmissionId("submission-1"),
            TimeEntrySubmissionScope.SelectedEntries,
            TimeEntryApprovalState.Submitted));
        return state;
    }

    private static TimeEntryState RejectedState(RecordTimeEntry command)
    {
        TimeEntryState state = SubmittedState(command);
        state.Apply(new TimeEntryRejected(
            command.TimeEntryId,
            new PartyReference("approver-1"),
            new TenantReference("tenant-1"),
            DecisionAtUtc(),
            new TimeEntryApprovalDecisionId("decision-1"),
            TimeEntryApprovalState.Rejected,
            new ApprovalAuthoritySourceAttribution(
                ApprovalAuthorityAction.EntryRejection,
                ApprovalAuthoritySource.ProjectApprover,
                ApprovalAuthorityDecisionState.Allowed,
                "timesheets.approval-authority.v1",
                "v1",
                ProjectionFreshnessMetadata.Fresh),
            TimeEntryApprovalScope.IndividualEntry,
            new TimeEntryRejectionReason("Needs customer PO evidence.")));
        return state;
    }

    private static TimeEntryState ApprovedState(RecordTimeEntry command)
    {
        TimeEntryState state = SubmittedState(command);
        state.Apply(new TimeEntryApproved(
            command.TimeEntryId,
            new PartyReference("approver-1"),
            new TenantReference("tenant-1"),
            DecisionAtUtc(),
            new TimeEntryApprovalDecisionId("decision-1"),
            TimeEntryApprovalState.Approved,
            new ApprovalAuthoritySourceAttribution(
                ApprovalAuthorityAction.EntryApproval,
                ApprovalAuthoritySource.ProjectApprover,
                ApprovalAuthorityDecisionState.Allowed,
                "timesheets.approval-authority.v1",
                "v1",
                ProjectionFreshnessMetadata.Fresh),
            TimeEntryApprovalScope.IndividualEntry));
        return state;
    }

    private static DateTimeOffset SubmittedAtUtc()
        => new(2026, 6, 19, 12, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset DecisionAtUtc()
        => new(2026, 6, 19, 13, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset CorrectionAtUtc()
        => new(2026, 6, 20, 9, 30, 0, TimeSpan.Zero);

    private static ApprovalAuthorityResolutionResult AllowedResolution(ApprovalAuthorityAction action)
        => ApprovalAuthorityResolutionResult.Allowed(new ApprovalAuthoritySourceAttribution(
            action,
            ApprovalAuthoritySource.ProjectApprover,
            ApprovalAuthorityDecisionState.Allowed,
            "timesheets.approval-authority.v1",
            "v1",
            ProjectionFreshnessMetadata.Fresh));

    private static ProjectReference Project() => new("project-1");

    private static WorkReference Work() => new("work-1");

    private static PartyReference Contributor() => new("party-1");

    private static ActivityTypeId ActivityId() => new("activity-type-1");

    private static TimeEntryId TimeEntryId() => new("time-entry-1");

    private static Fixture AuthorizedFixture()
    {
        Fixture fixture = new();
        fixture.TenantValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), TimesheetsOperation.Command, Arg.Any<CancellationToken>())
            .Returns(TimesheetsTenantAccessResult.Authorized());
        fixture.PolicyEvaluator
            .EvaluateAsync(Arg.Any<TimesheetsAuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(TimesheetsPolicyEvaluationResult.Allowed());
        return fixture;
    }

    private static Fixture AuthorizedProjectFixture()
    {
        Fixture fixture = AuthorizedFixture();
        fixture.ProjectValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<ProjectReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());
        fixture.PartyValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<PartyReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());
        return fixture;
    }

    private static Fixture AuthorizedProjectionReadFixture()
    {
        Fixture fixture = new();
        fixture.TenantValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), TimesheetsOperation.ProjectionRead, Arg.Any<CancellationToken>())
            .Returns(TimesheetsTenantAccessResult.Authorized());
        fixture.PolicyEvaluator
            .EvaluateAsync(Arg.Any<TimesheetsAuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(TimesheetsPolicyEvaluationResult.Allowed());
        return fixture;
    }

    private static TimesheetsRejection Rejection(TimeEntryCommandResult result)
    {
        result.Authorization.IsAuthorized.ShouldBeTrue();
        result.DomainResult.ShouldNotBeNull();
        result.DomainResult.IsRejection.ShouldBeTrue();
        return result.DomainResult.Events.ShouldHaveSingleItem().ShouldBeOfType<TimesheetsRejection>();
    }

    private static TimesheetsRejection SubmissionRejection(TimeEntrySubmissionEntryResult result)
    {
        result.Authorization.IsAuthorized.ShouldBeTrue();
        result.DomainResult.ShouldNotBeNull();
        result.DomainResult.IsRejection.ShouldBeTrue();
        return result.DomainResult.Events.ShouldHaveSingleItem().ShouldBeOfType<TimesheetsRejection>();
    }

    private static TimesheetsRejection CorrectionRejection(TimeEntryCorrectionCommandResult result)
    {
        result.CurrentAuthorization.IsAuthorized.ShouldBeTrue();
        result.CorrectedAuthorization.ShouldNotBeNull().IsAuthorized.ShouldBeTrue();
        result.AuthorityResolution.ShouldNotBeNull().IsAllowed.ShouldBeTrue();
        result.DomainResult.ShouldNotBeNull();
        result.DomainResult.IsRejection.ShouldBeTrue();
        return result.DomainResult.Events.ShouldHaveSingleItem().ShouldBeOfType<TimesheetsRejection>();
    }

    private sealed class Fixture
    {
        public ITimesheetsTenantAccessValidator TenantValidator { get; } = Substitute.For<ITimesheetsTenantAccessValidator>();

        public IProjectReferenceValidator ProjectValidator { get; } = Substitute.For<IProjectReferenceValidator>();

        public IWorkReferenceValidator WorkValidator { get; } = Substitute.For<IWorkReferenceValidator>();

        public IContributorPartyValidator PartyValidator { get; } = Substitute.For<IContributorPartyValidator>();

        public ITimesheetsPolicyEvaluator PolicyEvaluator { get; } = Substitute.For<ITimesheetsPolicyEvaluator>();

        public ITimeEntryEvidenceProjectionReader ProjectionReader { get; } = Substitute.For<ITimeEntryEvidenceProjectionReader>();

        public ITimeEntryDisplayHydrator DisplayHydrator { get; } = Substitute.For<ITimeEntryDisplayHydrator>();

        public ITimesheetsApprovalAuthorityResolver ApprovalResolver { get; } = Substitute.For<ITimesheetsApprovalAuthorityResolver>();

        public TimeEntryCommandService CreateService()
            => new(new TimesheetsAccessGuard(
                TenantValidator,
                ProjectValidator,
                WorkValidator,
                PartyValidator,
                PolicyEvaluator));

        public TimeEntrySubmissionCommandService CreateSubmissionService()
            => new(new TimesheetsAccessGuard(
                TenantValidator,
                ProjectValidator,
                WorkValidator,
                PartyValidator,
                PolicyEvaluator));

        public TimeEntryApprovalCommandService CreateApprovalService()
            => CreateApprovalService(ApprovalResolver);

        public TimeEntryApprovalCommandService CreateApprovalService(ITimesheetsApprovalAuthorityResolver resolver)
            => new(
                new TimesheetsAccessGuard(
                    TenantValidator,
                    ProjectValidator,
                    WorkValidator,
                    PartyValidator,
                    PolicyEvaluator),
                resolver);

        public TimeEntryCorrectionCommandService CreateCorrectionService()
            => CreateCorrectionService(ApprovalResolver);

        public TimeEntryCorrectionCommandService CreateCorrectionService(ITimesheetsApprovalAuthorityResolver resolver)
            => new(
                new TimesheetsAccessGuard(
                    TenantValidator,
                    ProjectValidator,
                    WorkValidator,
                    PartyValidator,
                    PolicyEvaluator),
                resolver);

        public TimeEntryEvidenceQueryService CreateQueryService()
            => new(
                new TimesheetsAccessGuard(
                    TenantValidator,
                    ProjectValidator,
                    WorkValidator,
                    PartyValidator,
                    PolicyEvaluator),
                ProjectionReader,
                DisplayHydrator);
    }

    private sealed class AllowingAuthorityProvider(ApprovalAuthoritySource source) : IApprovalAuthoritySourceProvider
    {
        public ApprovalAuthoritySource Source { get; } = source;

        public int Precedence => TimesheetsApprovalAuthorityPolicyOptions.DefaultPrecedence(Source);

        public ValueTask<ApprovalAuthoritySourceResult> EvaluateAsync(
            ApprovalAuthorityResolutionRequest request,
            CancellationToken cancellationToken)
        {
            return ValueTask.FromResult(ApprovalAuthoritySourceResult.Allowed(
                Source,
                ProjectionFreshnessMetadata.Fresh));
        }
    }
}
