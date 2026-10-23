using Hexalith.Timesheets.Contracts.Commands.TimeEntries;
using Hexalith.Timesheets.Contracts.Events.Rejections;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Policies;
using Hexalith.Timesheets.Contracts.Queries.TimeEntries;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.ApprovalAuthority;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.Policies;
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
    public async Task CaptureFailsClosedOnTenantAuthorityBeforeTargetContributorPolicyOrDomainDispatch(
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
    public async Task ProjectTargetedCaptureFailsClosedBeforeDomainDispatchWhenProjectAuthorityIsInvalid(
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
    public async Task CaptureFailsClosedOnContributorAuthorityBeforePolicyOrDomainDispatch(
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
    public async Task ProjectTargetedCaptureValidatesTenantProjectContributorPolicyThenDispatches()
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
    public async Task AiAgentCaptureUsesExistingTenantProjectContributorPolicyAndCatalogGates()
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
    public async Task CapturePolicyDenialFailsBeforeActivityTypeSelectionOrDomainDispatch()
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
    public async Task WorkTargetedCaptureValidatesOnlyWorkAndContributorBeforePolicy()
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
    public async Task CaptureRejectsStaleActivityTypeCatalogBeforeAggregateDispatch()
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
    public async Task CaptureRejectsMissingActivityTypeBeforeAggregateDispatch()
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
    public async Task CaptureRejectsInactiveActivityTypeBeforeAggregateDispatch()
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
    public async Task ProjectCaptureRejectsProjectActivityTypeFromDifferentProjectBeforeAggregateDispatch()
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
    public async Task WorkCaptureRejectsProjectActivityTypeUntilGoverningProjectAdapterExists()
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
    public async Task ProjectCaptureDispatchesWithMatchingProjectActivityTypeScope()
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
    public async Task SubmissionFailsClosedOnTenantAuthorityBeforeReferencePolicyCatalogOrDomainDispatch()
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
    public async Task ProjectSubmissionValidatesAuthorityAndDispatchesWithServerContext()
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
    public async Task ProjectSubmissionFailsClosedOnProjectAuthorityBeforeContributorPolicyOrDomainDispatch()
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
    public async Task SubmissionFailsClosedOnContributorAuthorityBeforePolicyOrDomainDispatch()
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
    public async Task SubmissionPolicyDenialFailsBeforeCatalogOrDomainDispatch()
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
    public async Task SubmissionRejectsStaleActivityTypeCatalogBeforeDomainDispatch()
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
    public async Task SubmissionRejectsInactiveActivityTypeBeforeDomainDispatch()
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
    public async Task WorkSubmissionRejectsProjectActivityTypeUntilGoverningProjectAdapterExists()
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
    public async Task SubmissionPartialBatchReportsAcceptedAndBlockedEntries()
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
    public async Task SubmissionDeduplicatesRepeatedTimeEntryIdsIntoASingleTransition()
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
    public async Task ApprovalFailsClosedOnBaseAuthorityBeforeResolverOrDomainDispatch()
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
    public async Task ProjectApprovalUsesSameContributorSourceForAccessAndResolverThenDispatches()
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
    public async Task RejectionUsesEntryRejectionAuthorityActionAndDispatchesRejectedEvent()
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
    public async Task ApprovalAuthorityDenialUsesSafeCopyAndDoesNotDispatchDomain()
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
    public async Task ApprovalServiceDeniesSelfApprovalByDefaultThroughResolver()
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
    public async Task ApprovalServiceAllowsSelfApprovalOnlyWhenPolicyExplicitlyAllowsEntryApproval()
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
    public async Task CorrectionFailsClosedOnCurrentEntryAuthorityBeforeCorrectedReferenceOrDomainDispatch()
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
    public async Task ApprovedEntryCorrectionAttemptFailsClosedOnCurrentAuthorityBeforeLockGuardDispatch()
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
    public async Task CorrectionValidatesCurrentAndCorrectedReferencesThenUsesCorrectionAuthorityAction()
    {
        Fixture fixture = AuthorizedProjectFixture();
        fixture.ApprovalResolver
            .ResolveAsync(Arg.Any<ApprovalAuthorityResolutionRequest>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(AllowedResolution(ApprovalAuthorityAction.CorrectionAuthorization)));

        TimeEntryCorrectionCommandResult result = await fixture.CreateCorrectionService().CorrectAsync(
            Context(),
            CorrectCommand(TimeEntryId()) with
            {
                Comment = new TimeEntryComment("correction private note",
                    TimeEntryCommentPolicy.SensitiveDefault with
                    { ProjectionInclusion = TimesheetsCommentPolicyDecision.Allowed })
            },
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
        corrected.CorrectedValues.Comment.ShouldNotBeNull().Policy.ServerPublicationVersion
            .ShouldBe(TimeEntryCommentPublicationIssuer.CurrentVersion);
        corrected.CorrectedValues.Comment.Policy.ServerProjectionInclusion
            .ShouldBe(TimesheetsCommentPolicyDecision.PolicyRequired);
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
    public async Task ApprovedCorrectionValidatesCurrentAndCorrectedReferencesThenDispatchesAdditiveEvent()
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
    public async Task CorrectionFailsClosedOnCorrectedTargetAuthorityBeforeResolverOrDomainDispatch()
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
    public async Task CorrectionAuthorityDenialUsesSafeCopyAndDoesNotDispatchDomain()
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
    public async Task CorrectionRejectsStaleActivityTypeCatalogBeforeDomainDispatch()
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
    public async Task CorrectionAuthorityIsProviderResolvedAndFailsClosedForSelfCorrectionWithoutAGrantingProvider()
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
    public async Task EvidenceQueryFailsClosedBeforeProjectionLookupWhenTenantAuthorityIsMissing()
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
    public async Task EvidenceQueryReturnsNonDisclosingNotFoundWhenProjectionHasNoEntry()
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
    public async Task EvidenceQueryFailsClosedOnProjectAuthorityBeforeReturningIdentifiersLineageOrLabels(
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
    public async Task EvidenceQueryFailsClosedOnContributorAuthorityBeforeReturningLineageOrLabels(
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
    public async Task EvidenceQueryPolicyDenialAfterReferenceValidationReturnsNoEvidence()
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
    public async Task EvidenceQueryValidatesWorkTargetWithoutProjectLookup()
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
    public async Task EvidenceQueryValidatesProjectAndContributorThenReturnsReadTimeHydratedEvidence()
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

    [Fact]
    public async Task EvidenceListQueryFailsClosedBeforeProjectionLookupWhenTenantAuthorityIsMissing()
    {
        Fixture fixture = new();
        fixture.TenantValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), TimesheetsOperation.ProjectionRead, Arg.Any<CancellationToken>())
            .Returns(TimesheetsTenantAccessResult.Denied(
                TimesheetsTenantAccessState.MissingTenant,
                "Authority cannot be resolved."));

        TimeEntryEvidenceListQueryResult result = await fixture.CreateListQueryService().QueryAsync(
            Context(),
            new QueryTimeEntries(),
            TestContext.Current.CancellationToken);

        result.WasDisclosed.ShouldBeFalse();
        result.Page.ShouldBeNull();
        result.Authorization.DenialCategory.ShouldBe(TimesheetsDenialCategory.MissingTenant);
        fixture.ListProjectionReader.ReceivedCalls().ShouldBeEmpty();
        fixture.DisplayHydrator.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task EvidenceListQueryFiltersOrdinaryUnauthorizedRowsAndHydratesOnlyDisclosedRows()
    {
        Fixture fixture = AuthorizedProjectionReadFixture();
        ProjectReference deniedProject = new("project-denied");
        fixture.ListProjectionReader
            .QueryAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<QueryTimeEntries>(), Arg.Any<CancellationToken>())
            .Returns(new TimeEntryQueryReadModel(
                [
                    QueryRow("time-entry-1", TimeEntryTargetReference.ForProject(Project())),
                    QueryRow("time-entry-2", TimeEntryTargetReference.ForProject(deniedProject))
                ],
                null,
                ProjectionFreshnessMetadata.Fresh));
        fixture.ProjectValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Project(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());
        fixture.ProjectValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), deniedProject, Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Denied(ReferenceValidationState.Unauthorized, "Project is not visible."));
        fixture.PartyValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<PartyReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());
        fixture.DisplayHydrator
            .HydrateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<TimeEntryEvidenceReadModel>(), Arg.Any<CancellationToken>())
            .Returns(new TimeEntryDisplayHydration(
                TimeEntryHydratedDisplayLabel.Fresh("Contributor"),
                TimeEntryHydratedDisplayLabel.Fresh("Project"),
                TimeEntryHydratedDisplayLabel.Unavailable()));

        TimeEntryEvidenceListQueryResult result = await fixture.CreateListQueryService().QueryAsync(
            Context(),
            new QueryTimeEntries(),
            TestContext.Current.CancellationToken);

        result.WasDisclosed.ShouldBeTrue();
        TimeEntryQueryRowReadModel row = result.Page.ShouldNotBeNull().Items.ShouldHaveSingleItem();
        row.TimeEntryId.ShouldBe(TimeEntryId());
        row.DisplayHydration.Target.Label.ShouldBe("Project");
        await fixture.ProjectValidator.Received(1)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Project(), Arg.Any<CancellationToken>());
        await fixture.ProjectValidator.Received(1)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), deniedProject, Arg.Any<CancellationToken>());
        await fixture.DisplayHydrator.Received(1)
            .HydrateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<TimeEntryEvidenceReadModel>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EvidenceListQueryReturnsAuthorizedEmptyPageWithoutHydratingLabels()
    {
        Fixture fixture = AuthorizedProjectionReadFixture();
        fixture.ListProjectionReader
            .QueryAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<QueryTimeEntries>(), Arg.Any<CancellationToken>())
            .Returns(new TimeEntryQueryReadModel([], null, ProjectionFreshnessMetadata.Fresh));

        TimeEntryEvidenceListQueryResult result = await fixture.CreateListQueryService().QueryAsync(
            Context(),
            new QueryTimeEntries(),
            TestContext.Current.CancellationToken);

        result.WasDisclosed.ShouldBeTrue();
        result.Page.ShouldNotBeNull().Items.ShouldBeEmpty();
        fixture.ProjectValidator.ReceivedCalls().ShouldBeEmpty();
        fixture.WorkValidator.ReceivedCalls().ShouldBeEmpty();
        fixture.PartyValidator.ReceivedCalls().ShouldBeEmpty();
        fixture.DisplayHydrator.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task EvidenceListQueryFailsClosedForCrossTenantRowWithoutHydratingLabels()
    {
        Fixture fixture = AuthorizedProjectionReadFixture();
        fixture.ListProjectionReader
            .QueryAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<QueryTimeEntries>(), Arg.Any<CancellationToken>())
            .Returns(new TimeEntryQueryReadModel(
                [QueryRow("time-entry-1", TimeEntryTargetReference.ForProject(Project()))],
                null,
                ProjectionFreshnessMetadata.Fresh));
        fixture.ProjectValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<ProjectReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Denied(
                ReferenceValidationState.TenantMismatch,
                "Project belongs to a different tenant."));

        TimeEntryEvidenceListQueryResult result = await fixture.CreateListQueryService().QueryAsync(
            Context(),
            new QueryTimeEntries(),
            TestContext.Current.CancellationToken);

        result.WasDisclosed.ShouldBeFalse();
        result.Page.ShouldBeNull();
        result.Authorization.DenialCategory.ShouldBe(TimesheetsDenialCategory.CrossTenantTarget);
        fixture.DisplayHydrator.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task EvidenceListQueryValidatesWorkTargetWithoutProjectLookup()
    {
        Fixture fixture = AuthorizedProjectionReadFixture();
        fixture.ListProjectionReader
            .QueryAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<QueryTimeEntries>(), Arg.Any<CancellationToken>())
            .Returns(new TimeEntryQueryReadModel(
                [QueryRow("time-entry-1", TimeEntryTargetReference.ForWork(Work()))],
                null,
                ProjectionFreshnessMetadata.Fresh));
        fixture.WorkValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Work(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());
        fixture.PartyValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Contributor(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());
        fixture.DisplayHydrator
            .HydrateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<TimeEntryEvidenceReadModel>(), Arg.Any<CancellationToken>())
            .Returns(new TimeEntryDisplayHydration(
                TimeEntryHydratedDisplayLabel.Fresh("Contributor"),
                TimeEntryHydratedDisplayLabel.Fresh("Work"),
                TimeEntryHydratedDisplayLabel.Unavailable()));

        TimeEntryEvidenceListQueryResult result = await fixture.CreateListQueryService().QueryAsync(
            Context(),
            new QueryTimeEntries(),
            TestContext.Current.CancellationToken);

        result.WasDisclosed.ShouldBeTrue();
        TimeEntryQueryRowReadModel row = result.Page.ShouldNotBeNull().Items.ShouldHaveSingleItem();
        row.Target.TargetKind.ShouldBe(TimeEntryTargetKind.Work);
        row.DisplayHydration.Target.Label.ShouldBe("Work");
        fixture.ProjectValidator.ReceivedCalls().ShouldBeEmpty();
        await fixture.WorkValidator.Received(1)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Work(), Arg.Any<CancellationToken>());
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

    private static TimeEntryQueryRowReadModel QueryRow(string timeEntryId, TimeEntryTargetReference target)
        => new(
            new TimeEntryId(timeEntryId),
            target,
            Contributor(),
            ActivityId(),
            ActivityTypeScope.Tenant,
            new DateOnly(2026, 6, 19),
            60,
            BillableState.Billable,
            TimeEntryApprovalState.Draft,
            TimeEntryCorrectionState.None,
            ContributorCategory.Employee,
            TimeEntrySourceType.Employee,
            ProjectionFreshnessMetadata.Fresh);

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

        public ITimeEntryEvidenceListProjectionReader ListProjectionReader { get; } = Substitute.For<ITimeEntryEvidenceListProjectionReader>();

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

        public TimeEntryEvidenceListQueryService CreateListQueryService()
            => new(
                new TimesheetsAccessGuard(
                    TenantValidator,
                    ProjectValidator,
                    WorkValidator,
                    PartyValidator,
                    PolicyEvaluator),
                ListProjectionReader,
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
