using Hexalith.Timesheets.Contracts.Commands.ExternalContributions;
using Hexalith.Timesheets.Contracts.Commands.TimeEntries;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.References;
using Hexalith.Timesheets.Server.TimeEntries;

using NSubstitute;

using Shouldly;

namespace Hexalith.Timesheets.Server.Tests;

public sealed class ExternalContributionCommandServiceTests
{
    [Fact]
    public async Task Submit_external_time_entry_reuses_record_flow_and_forces_external_contributor_evidence()
    {
        Fixture fixture = AuthorizedProjectFixture();

        ExternalContributionCommandResult result = await fixture.CreateExternalService().SubmitAsync(
            Context(),
            SubmitExternalCommand(),
            null,
            FreshCatalog(),
            EventAtUtc(),
            TestContext.Current.CancellationToken);

        result.RecordResult.WasDispatched.ShouldBeTrue();
        result.SubmissionResult.ShouldBeNull();
        TimeEntryRecorded recorded = result.RecordResult.DomainResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimeEntryRecorded>();
        recorded.ContributorCategory.ShouldBe(ContributorCategory.ExternalContributor);
        recorded.AiMetrics.ShouldBeNull();
        recorded.ExternalSource.ShouldBe(new ExternalContributionSource("supplier-api", "request-1"));
    }

    [Fact]
    public async Task Submit_external_time_entry_fails_closed_before_dispatch_when_project_authority_is_invalid()
    {
        Fixture fixture = AuthorizedFixture();
        fixture.ProjectValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<ProjectReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Denied(
                ReferenceValidationState.Stale,
                "Project authority cannot be resolved."));

        ExternalContributionCommandResult result = await fixture.CreateExternalService().SubmitAsync(
            Context(),
            SubmitExternalCommand(),
            null,
            FreshCatalog(),
            EventAtUtc(),
            TestContext.Current.CancellationToken);

        result.RecordResult.WasDispatched.ShouldBeFalse();
        result.RecordResult.DomainResult.ShouldBeNull();
        result.RecordResult.Authorization.DenialCategory.ShouldBe(TimesheetsDenialCategory.StaleProjection);
        result.SubmissionResult.ShouldBeNull();
        await fixture.ProjectValidator.Received(1)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Project(), Arg.Any<CancellationToken>());
        fixture.PolicyEvaluator.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Submit_external_time_entry_fails_closed_on_tenant_authority_before_references_or_dispatch()
    {
        Fixture fixture = new();
        fixture.TenantValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), TimesheetsOperation.Command, Arg.Any<CancellationToken>())
            .Returns(TimesheetsTenantAccessResult.Denied(
                TimesheetsTenantAccessState.MissingTenant,
                "Authority cannot be resolved."));

        ExternalContributionCommandResult result = await fixture.CreateExternalService().SubmitAsync(
            Context(),
            SubmitExternalCommand(),
            null,
            FreshCatalog(),
            EventAtUtc(),
            TestContext.Current.CancellationToken);

        result.RecordResult.WasDispatched.ShouldBeFalse();
        result.RecordResult.Authorization.DenialCategory.ShouldBe(TimesheetsDenialCategory.MissingTenant);
        fixture.ProjectValidator.ReceivedCalls().ShouldBeEmpty();
        fixture.PartyValidator.ReceivedCalls().ShouldBeEmpty();
        fixture.PolicyEvaluator.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Submit_external_time_entry_fails_closed_on_invalid_contributor_before_dispatch()
    {
        Fixture fixture = AuthorizedFixture();
        fixture.ProjectValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<ProjectReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());
        fixture.PartyValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<PartyReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Denied(
                ReferenceValidationState.InvalidReference,
                "Contributor authority cannot be resolved."));

        ExternalContributionCommandResult result = await fixture.CreateExternalService().SubmitAsync(
            Context(),
            SubmitExternalCommand(),
            null,
            FreshCatalog(),
            EventAtUtc(),
            TestContext.Current.CancellationToken);

        result.RecordResult.WasDispatched.ShouldBeFalse();
        result.RecordResult.Authorization.DenialCategory.ShouldBe(TimesheetsDenialCategory.InvalidReference);
        await fixture.ProjectValidator.Received(1)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Project(), Arg.Any<CancellationToken>());
        await fixture.PartyValidator.Received(1)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Contributor(), Arg.Any<CancellationToken>());
        fixture.PolicyEvaluator.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Submit_external_time_entry_rejects_inactive_activity_type_before_aggregate_dispatch()
    {
        Fixture fixture = AuthorizedProjectFixture();
        ActivityTypeCatalogReadModel inactiveCatalog = new(
            [
                new(
                    ActivityId(),
                    ActivityTypeScope.Tenant,
                    null,
                    "Delivery",
                    false,
                    BillableState.Billable)
            ],
            ProjectionFreshnessMetadata.Fresh);

        ExternalContributionCommandResult result = await fixture.CreateExternalService().SubmitAsync(
            Context(),
            SubmitExternalCommand(),
            null,
            inactiveCatalog,
            EventAtUtc(),
            TestContext.Current.CancellationToken);

        result.RecordResult.WasDispatched.ShouldBeFalse();
        result.RecordResult.DomainResult.ShouldNotBeNull().IsRejection.ShouldBeTrue();
    }

    [Fact]
    public async Task Submit_external_time_entry_same_source_retry_is_noop_without_duplicate_record_event()
    {
        Fixture fixture = AuthorizedProjectFixture();
        TimeEntryState state = RecordedExternalState();

        ExternalContributionCommandResult result = await fixture.CreateExternalService().SubmitAsync(
            Context(),
            SubmitExternalCommand(),
            state,
            FreshCatalog(),
            EventAtUtc(),
            TestContext.Current.CancellationToken);

        result.RecordResult.WasDispatched.ShouldBeTrue();
        result.RecordResult.DomainResult.ShouldNotBeNull().IsNoOp.ShouldBeTrue();
        result.SubmissionResult.ShouldBeNull();
    }

    [Fact]
    public async Task Submit_external_time_entry_submitted_policy_reuses_submission_flow()
    {
        Fixture fixture = AuthorizedProjectFixture();
        ExternalContributionPolicyOptions options = new()
        {
            InitialApprovalState = TimeEntryApprovalState.Submitted
        };

        ExternalContributionCommandResult result = await fixture.CreateExternalService(options).SubmitAsync(
            Context(),
            SubmitExternalCommand(),
            null,
            FreshCatalog(),
            EventAtUtc(),
            TestContext.Current.CancellationToken);

        result.RecordResult.WasDispatched.ShouldBeTrue();
        result.RecordResult.DomainResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimeEntryRecorded>();
        TimeEntrySubmissionEntryResult submittedEntry = result.SubmissionResult.ShouldNotBeNull()
            .Entries.ShouldHaveSingleItem();
        submittedEntry.WasDispatched.ShouldBeTrue();
        TimeEntrySubmitted submitted = submittedEntry.DomainResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimeEntrySubmitted>();
        submitted.TimeEntryId.ShouldBe(TimeEntryId());
        submitted.Submitter.ShouldBe(Context().Actor);
        submitted.Tenant.ShouldBe(Context().Tenant);
        submitted.TimeEntrySubmissionId.ShouldBe(new TimeEntrySubmissionId("request-1"));
        submitted.SubmissionScope.ShouldBe(TimeEntrySubmissionScope.SelectedEntries);
        submitted.ApprovalState.ShouldBe(TimeEntryApprovalState.Submitted);
    }

    [Fact]
    public async Task Submit_external_time_entry_fails_closed_on_work_authority_before_contributor_policy_or_dispatch()
    {
        Fixture fixture = AuthorizedFixture();
        fixture.WorkValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<WorkReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Denied(
                ReferenceValidationState.Unavailable,
                "Work authority cannot be resolved."));

        ExternalContributionCommandResult result = await fixture.CreateExternalService().SubmitAsync(
            Context(),
            SubmitExternalWorkCommand(),
            null,
            FreshCatalog(),
            EventAtUtc(),
            TestContext.Current.CancellationToken);

        result.RecordResult.WasDispatched.ShouldBeFalse();
        result.RecordResult.DomainResult.ShouldBeNull();
        result.RecordResult.Authorization.DenialCategory.ShouldBe(TimesheetsDenialCategory.UnavailableSiblingAuthority);
        result.SubmissionResult.ShouldBeNull();
        fixture.ProjectValidator.ReceivedCalls().ShouldBeEmpty();
        await fixture.WorkValidator.Received(1)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Work(), Arg.Any<CancellationToken>());
        fixture.PartyValidator.ReceivedCalls().ShouldBeEmpty();
        fixture.PolicyEvaluator.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task Submit_external_time_entry_fails_closed_on_policy_denial_before_dispatch()
    {
        Fixture fixture = AuthorizedProjectFixture();
        fixture.PolicyEvaluator
            .EvaluateAsync(Arg.Any<TimesheetsAuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(TimesheetsPolicyEvaluationResult.Denied(
                TimesheetsDenialCategory.UnconfiguredPolicy,
                "External contribution policy cannot be resolved."));

        ExternalContributionCommandResult result = await fixture.CreateExternalService().SubmitAsync(
            Context(),
            SubmitExternalCommand(),
            null,
            FreshCatalog(),
            EventAtUtc(),
            TestContext.Current.CancellationToken);

        result.RecordResult.WasDispatched.ShouldBeFalse();
        result.RecordResult.DomainResult.ShouldBeNull();
        result.RecordResult.Authorization.DenialCategory.ShouldBe(TimesheetsDenialCategory.UnconfiguredPolicy);
        result.SubmissionResult.ShouldBeNull();
    }

    [Fact]
    public async Task Confirm_external_time_entry_records_confirmation_evidence_not_approval()
    {
        Fixture fixture = AuthorizedProjectFixture();

        TimeEntryConfirmationCommandResult result = await fixture.CreateExternalService().ConfirmAsync(
            Context(),
            ConfirmExternalCommand(),
            RecordedExternalState(),
            EventAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeTrue();
        TimeEntryContributorConfirmed confirmed = result.DomainResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimeEntryContributorConfirmed>();
        confirmed.Contributor.ShouldBe(Contributor());
        confirmed.Tenant.ShouldBe(Context().Tenant);
        confirmed.Source.ShouldBe(new ExternalContributionSource("supplier-api", "confirm-1"));
        result.DomainResult.Events.ShouldNotContain(static @event => @event is TimeEntryApproved);
    }

    [Fact]
    public async Task Confirm_external_time_entry_same_source_retry_is_noop_without_duplicate_confirmation_event()
    {
        Fixture fixture = AuthorizedProjectFixture();
        TimeEntryState state = ConfirmedExternalState();

        TimeEntryConfirmationCommandResult result = await fixture.CreateExternalService().ConfirmAsync(
            Context(),
            ConfirmExternalCommand(),
            state,
            EventAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeTrue();
        result.DomainResult.ShouldNotBeNull().IsNoOp.ShouldBeTrue();
        result.DomainResult.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task Confirm_external_time_entry_fails_closed_on_invalid_contributor_before_dispatch()
    {
        Fixture fixture = AuthorizedFixture();
        fixture.ProjectValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<ProjectReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Valid());
        fixture.PartyValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<PartyReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Denied(
                ReferenceValidationState.InvalidReference,
                "Contributor authority cannot be resolved."));

        TimeEntryConfirmationCommandResult result = await fixture.CreateExternalService().ConfirmAsync(
            Context(),
            ConfirmExternalCommand(),
            RecordedExternalState(),
            EventAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeFalse();
        result.Authorization.DenialCategory.ShouldBe(TimesheetsDenialCategory.InvalidReference);
        result.DomainResult.ShouldBeNull();
    }

    private static SubmitExternalTimeEntry SubmitExternalCommand()
        => new(
            TimeEntryId(),
            TimeEntryTargetReference.ForProject(Project()),
            Contributor(),
            ActivityId(),
            new DateOnly(2026, 6, 19),
            60,
            BillableState.Billable,
            new ExternalContributionSource("supplier-api", "request-1"));

    private static SubmitExternalTimeEntry SubmitExternalWorkCommand()
        => SubmitExternalCommand() with
        {
            Target = TimeEntryTargetReference.ForWork(Work())
        };

    private static ConfirmExternalTimeEntry ConfirmExternalCommand()
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

    private static TimeEntryState RecordedExternalState()
    {
        TimeEntryState state = new();
        state.Apply(new TimeEntryRecorded(
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
        });
        return state;
    }

    private static TimeEntryState ConfirmedExternalState()
    {
        TimeEntryState state = RecordedExternalState();
        state.Apply(new TimeEntryContributorConfirmed(
            TimeEntryId(),
            Contributor(),
            Context().Tenant!,
            EventAtUtc(),
            new ExternalContributionSource("supplier-api", "confirm-1")));
        return state;
    }

    private static TimesheetsRequestContext Context()
        => new(
            new TenantReference("tenant-1"),
            new PartyReference("operator-1"),
            "correlation-1");

    private static DateTimeOffset EventAtUtc()
        => new(2026, 6, 19, 12, 0, 0, TimeSpan.Zero);

    private static ProjectReference Project() => new("project-1");

    private static WorkReference Work() => new("work-1");

    private static PartyReference Contributor() => new("party-1");

    private static ActivityTypeId ActivityId() => new("activity-type-1");

    private static TimeEntryId TimeEntryId() => new("time-entry-1");

    private static Fixture AuthorizedFixture()
    {
        Fixture fixture = new();
        fixture.TenantValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<TimesheetsOperation>(), Arg.Any<CancellationToken>())
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

    private sealed class Fixture
    {
        public ITimesheetsTenantAccessValidator TenantValidator { get; } = Substitute.For<ITimesheetsTenantAccessValidator>();

        public IProjectReferenceValidator ProjectValidator { get; } = Substitute.For<IProjectReferenceValidator>();

        public IWorkReferenceValidator WorkValidator { get; } = Substitute.For<IWorkReferenceValidator>();

        public IContributorPartyValidator PartyValidator { get; } = Substitute.For<IContributorPartyValidator>();

        public ITimesheetsPolicyEvaluator PolicyEvaluator { get; } = Substitute.For<ITimesheetsPolicyEvaluator>();

        public ExternalContributionCommandService CreateExternalService(
            ExternalContributionPolicyOptions? policyOptions = null)
        {
            TimesheetsAccessGuard accessGuard = new(
                TenantValidator,
                ProjectValidator,
                WorkValidator,
                PartyValidator,
                PolicyEvaluator);

            return new(
                new TimeEntryCommandService(accessGuard),
                new TimeEntrySubmissionCommandService(accessGuard),
                accessGuard,
                policyOptions ?? ExternalContributionPolicyOptions.Default);
        }
    }
}
