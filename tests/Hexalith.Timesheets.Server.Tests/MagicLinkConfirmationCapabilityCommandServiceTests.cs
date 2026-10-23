using Hexalith.Timesheets.Contracts.Commands.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.Rejections;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.Policies;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.MagicLinks;
using Hexalith.Timesheets.Server.Policies;
using Hexalith.Timesheets.Server.References;
using Hexalith.Timesheets.Server.TimeEntries;

using NSubstitute;

using Shouldly;

using CapabilityStateData = Hexalith.Timesheets.Server.MagicLinks.MagicLinkCapabilityState;

namespace Hexalith.Timesheets.Server.Tests;

public sealed partial class MagicLinkConfirmationCapabilityCommandServiceTests
{
    [Fact]
    public async Task ExistingTimeEntryIssuanceIsRejectedBeforeTokenGenerationAndByTheAggregate()
    {
        Fixture fixture = AuthorizedProjectFixture();
        IssueMagicLinkConfirmationCapability original = IssueCommand();
        var command = original with { Scope = original.Scope with { TargetKind = MagicLinkTargetKind.ExistingTimeEntry } };
        MagicLinkCapabilityCommandResult result = await fixture.CreateService().IssueAsync(Context(), command,
            null, FreshCatalog(), IssuedAtUtc(), TestContext.Current.CancellationToken);
        result.DomainResult.ShouldNotBeNull().IsRejection.ShouldBeTrue();
        result.IssueResponse.ShouldBeNull();
        fixture.TokenGenerator.GenerateCount.ShouldBe(0);
        MagicLinkConfirmationCapability.HandleIssue(command, null, Context().Tenant, Context().Actor,
            new("hash-only"), IssuedAtUtc()).IsSuccess.ShouldBeFalse();
    }

    [Fact]
    public async Task IssueMagicLinkStoresHashAndMetadataOnlyAndReturnsOneTimeToken()
    {
        Fixture fixture = AuthorizedProjectFixture();

        MagicLinkCapabilityCommandResult result = await fixture.CreateService().IssueAsync(
            Context(),
            IssueCommand(),
            null,
            FreshCatalog(),
            IssuedAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeTrue();
        result.IssueResponse.ShouldNotBeNull().OneTimeToken.ShouldBe("opaque-once");
        MagicLinkConfirmationCapabilityIssued issued = result.DomainResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<MagicLinkConfirmationCapabilityIssued>();
        issued.TokenHash.ShouldBe(new MagicLinkTokenHash("hash-only"));
        issued.Tenant.ShouldBe(Context().Tenant);
        issued.Issuer.ShouldBe(Context().Actor);
        issued.IsSingleUse.ShouldBeTrue();
        JsonContainsNoRawToken(issued).ShouldBeTrue();
        fixture.TokenGenerator.GenerateCount.ShouldBe(1);
    }

    [Fact]
    public async Task IssueMagicLinkFailsClosedOnTenantAuthorityBeforeReferencesOrTokenGeneration()
    {
        Fixture fixture = new();
        fixture.TenantValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), TimesheetsOperation.MagicLinkIssuance, Arg.Any<CancellationToken>())
            .Returns(TimesheetsTenantAccessResult.Denied(
                TimesheetsTenantAccessState.MissingTenant,
                "Authority cannot be resolved."));

        MagicLinkCapabilityCommandResult result = await fixture.CreateService().IssueAsync(
            Context(),
            IssueCommand(),
            null,
            FreshCatalog(),
            IssuedAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeFalse();
        result.DomainResult.ShouldBeNull();
        result.IssueResponse.ShouldBeNull();
        fixture.TokenGenerator.GenerateCount.ShouldBe(0);
        fixture.ProjectValidator.ReceivedCalls().ShouldBeEmpty();
        fixture.PartyValidator.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task IssueMagicLinkFailsClosedOnInvalidProjectBeforeContributorPolicyOrTokenGeneration()
    {
        Fixture fixture = AuthorizedFixture();
        fixture.ProjectValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<ProjectReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Denied(
                ReferenceValidationState.InvalidReference,
                "Project authority cannot be resolved."));

        MagicLinkCapabilityCommandResult result = await fixture.CreateService().IssueAsync(
            Context(),
            IssueCommand(),
            null,
            FreshCatalog(),
            IssuedAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeFalse();
        result.Authorization.DenialCategory.ShouldBe(TimesheetsDenialCategory.InvalidReference);
        result.IssueResponse.ShouldBeNull();
        fixture.TokenGenerator.GenerateCount.ShouldBe(0);
        await fixture.ProjectValidator.Received(1)
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Project(), Arg.Any<CancellationToken>());
        fixture.PartyValidator.ReceivedCalls().ShouldBeEmpty();
        fixture.PolicyEvaluator.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task IssueMagicLinkFailsClosedOnInvalidContributorBeforePolicyOrTokenGeneration()
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

        MagicLinkCapabilityCommandResult result = await fixture.CreateService().IssueAsync(
            Context(),
            IssueCommand(),
            null,
            FreshCatalog(),
            IssuedAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeFalse();
        result.Authorization.DenialCategory.ShouldBe(TimesheetsDenialCategory.InvalidReference);
        result.IssueResponse.ShouldBeNull();
        fixture.TokenGenerator.GenerateCount.ShouldBe(0);
        fixture.PolicyEvaluator.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task IssueMagicLinkFailsClosedOnInvalidWorkBeforeContributorPolicyOrTokenGeneration()
    {
        Fixture fixture = AuthorizedFixture();
        fixture.WorkValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<WorkReference>(), Arg.Any<CancellationToken>())
            .Returns(ReferenceValidationResult.Denied(
                ReferenceValidationState.Unavailable,
                "Work authority cannot be resolved."));

        MagicLinkCapabilityCommandResult result = await fixture.CreateService().IssueAsync(
            Context(),
            IssueWorkCommand(),
            null,
            FreshCatalog(),
            IssuedAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeFalse();
        result.Authorization.DenialCategory.ShouldBe(TimesheetsDenialCategory.UnavailableSiblingAuthority);
        result.IssueResponse.ShouldBeNull();
        fixture.TokenGenerator.GenerateCount.ShouldBe(0);
        fixture.ProjectValidator.ReceivedCalls().ShouldBeEmpty();
        fixture.PartyValidator.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task IssueMagicLinkFailsClosedOnPolicyDenialBeforeTokenGeneration()
    {
        Fixture fixture = AuthorizedProjectFixture();
        fixture.PolicyEvaluator
            .EvaluateAsync(Arg.Any<TimesheetsAuthorizationRequest>(), Arg.Any<CancellationToken>())
            .Returns(TimesheetsPolicyEvaluationResult.Denied(
                TimesheetsDenialCategory.UnconfiguredPolicy,
                "Magic-link policy cannot be resolved."));

        MagicLinkCapabilityCommandResult result = await fixture.CreateService().IssueAsync(
            Context(),
            IssueCommand(),
            null,
            FreshCatalog(),
            IssuedAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeFalse();
        result.Authorization.DenialCategory.ShouldBe(TimesheetsDenialCategory.UnconfiguredPolicy);
        result.IssueResponse.ShouldBeNull();
        fixture.TokenGenerator.GenerateCount.ShouldBe(0);
    }

    [Fact]
    public async Task IssueMagicLinkFailsClosedWhenActivityCatalogIsStale()
    {
        Fixture fixture = AuthorizedProjectFixture();

        MagicLinkCapabilityCommandResult result = await fixture.CreateService().IssueAsync(
            Context(),
            IssueCommand(),
            null,
            new ActivityTypeCatalogReadModel([], ProjectionFreshnessMetadata.Stale()),
            IssuedAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeTrue();
        result.DomainResult.ShouldNotBeNull().IsRejection.ShouldBeTrue();
        result.IssueResponse.ShouldBeNull();
        fixture.TokenGenerator.GenerateCount.ShouldBe(0);
    }

    [Fact]
    public async Task IssueMagicLinkRejectsInactiveOrAmbiguousActivityTypeBeforeTokenGeneration()
    {
        Fixture inactiveFixture = AuthorizedProjectFixture();
        Fixture ambiguousFixture = AuthorizedProjectFixture();
        ActivityTypeCatalogItem inactive = new(ActivityId(), ActivityTypeScope.Tenant, null, "Delivery", false, BillableState.Billable);
        ActivityTypeCatalogItem duplicate = new(ActivityId(), ActivityTypeScope.Tenant, null, "Delivery duplicate", true, BillableState.Billable);

        MagicLinkCapabilityCommandResult inactiveResult = await inactiveFixture.CreateService().IssueAsync(
            Context(),
            IssueCommand(),
            null,
            new ActivityTypeCatalogReadModel([inactive], ProjectionFreshnessMetadata.Fresh),
            IssuedAtUtc(),
            TestContext.Current.CancellationToken);
        MagicLinkCapabilityCommandResult ambiguousResult = await ambiguousFixture.CreateService().IssueAsync(
            Context(),
            IssueCommand(),
            null,
            new ActivityTypeCatalogReadModel([FreshCatalog().Items[0], duplicate], ProjectionFreshnessMetadata.Fresh),
            IssuedAtUtc(),
            TestContext.Current.CancellationToken);

        inactiveResult.DomainResult.ShouldNotBeNull().IsRejection.ShouldBeTrue();
        ambiguousResult.DomainResult.ShouldNotBeNull().IsRejection.ShouldBeTrue();
        inactiveFixture.TokenGenerator.GenerateCount.ShouldBe(0);
        ambiguousFixture.TokenGenerator.GenerateCount.ShouldBe(0);
    }

    [Fact]
    public async Task IssueMagicLinkFailsClosedOnMalformedScopeWithoutTarget()
    {
        Fixture fixture = AuthorizedProjectFixture();
        IssueMagicLinkConfirmationCapability command = IssueCommand() with
        {
            Scope = IssueCommand().Scope with { Target = null! }
        };

        MagicLinkCapabilityCommandResult result = await fixture.CreateService().IssueAsync(
            Context(),
            command,
            null,
            FreshCatalog(),
            IssuedAtUtc(),
            TestContext.Current.CancellationToken);

        result.DomainResult.ShouldNotBeNull().IsRejection.ShouldBeTrue();
        result.IssueResponse.ShouldBeNull();
        fixture.TokenGenerator.GenerateCount.ShouldBe(0);
    }

    [Theory]
    [InlineData(MagicLinkAllowedAction.Unknown)]
    [InlineData((MagicLinkAllowedAction)99)]
    public async Task IssueMagicLinkRejectsUndefinedAllowedActionBeforeTokenGeneration(MagicLinkAllowedAction action)
    {
        Fixture fixture = AuthorizedProjectFixture();
        IssueMagicLinkConfirmationCapability command = IssueCommand() with
        {
            AllowedAction = action
        };

        MagicLinkCapabilityCommandResult result = await fixture.CreateService().IssueAsync(
            Context(),
            command,
            null,
            FreshCatalog(),
            IssuedAtUtc(),
            TestContext.Current.CancellationToken);

        result.DomainResult.ShouldNotBeNull().IsRejection.ShouldBeTrue();
        result.IssueResponse.ShouldBeNull();
    }

    [Fact]
    public async Task ExpireMagicLinkRejectsPrematureExpiryBeforeExpiryInstant()
    {
        MagicLinkCapabilityCommandResult result = await AuthorizedProjectFixture().CreateService().ExpireAsync(
            Context(),
            ExpireCommand(),
            IssuedState(),
            IssuedAtUtc(),
            TestContext.Current.CancellationToken);

        result.DomainResult.ShouldNotBeNull().IsRejection.ShouldBeTrue();
    }

    [Fact]
    public async Task IssueMagicLinkRejectsNonUtcOrExpiredAtIssueExpiry()
    {
        Fixture fixture = AuthorizedProjectFixture();
        IssueMagicLinkConfirmationCapability command = IssueCommand() with
        {
            ExpiresAtUtc = new DateTimeOffset(2026, 6, 19, 13, 0, 0, TimeSpan.FromHours(2))
        };

        MagicLinkCapabilityCommandResult result = await fixture.CreateService().IssueAsync(
            Context(),
            command,
            null,
            FreshCatalog(),
            IssuedAtUtc(),
            TestContext.Current.CancellationToken);

        result.DomainResult.ShouldNotBeNull().IsRejection.ShouldBeTrue();
        result.IssueResponse.ShouldBeNull();
    }

    [Fact]
    public async Task IssueMagicLinkRejectsDuplicateCapabilityWithoutGeneratingNewToken()
    {
        Fixture fixture = AuthorizedProjectFixture();
        CapabilityStateData state = IssuedState();

        MagicLinkCapabilityCommandResult result = await fixture.CreateService().IssueAsync(
            Context(),
            IssueCommand(),
            state,
            FreshCatalog(),
            IssuedAtUtc(),
            TestContext.Current.CancellationToken);

        result.DomainResult.ShouldNotBeNull().IsRejection.ShouldBeTrue();
        result.IssueResponse.ShouldBeNull();
        fixture.TokenGenerator.GenerateCount.ShouldBe(0);
    }

    [Fact]
    public async Task RevokeMagicLinkRecordsAuditableTerminalState()
    {
        Fixture fixture = AuthorizedProjectFixture();

        MagicLinkCapabilityCommandResult result = await fixture.CreateService().RevokeAsync(
            Context(),
            RevokeCommand(),
            IssuedState(),
            RevokedAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeTrue();
        MagicLinkConfirmationCapabilityRevoked revoked = result.DomainResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<MagicLinkConfirmationCapabilityRevoked>();
        revoked.RevokedBy.ShouldBe(Context().Actor);
        revoked.Source.ShouldBe(new MagicLinkAuditMetadata("timesheets", "revoke-1"));
    }

    [Fact]
    public async Task ExpireMagicLinkRecordsAuditablePolicyState()
    {
        MagicLinkCapabilityCommandResult result = await AuthorizedProjectFixture().CreateService().ExpireAsync(
            Context(),
            ExpireCommand(),
            IssuedState(),
            ExpiresAtUtc(),
            TestContext.Current.CancellationToken);

        result.Authorization.IsAuthorized.ShouldBeTrue();
        MagicLinkConfirmationCapabilityExpired expired = result.DomainResult.ShouldNotBeNull().Events.ShouldHaveSingleItem()
            .ShouldBeOfType<MagicLinkConfirmationCapabilityExpired>();
        expired.ExpiredAtUtc.ShouldBe(ExpiresAtUtc());
    }

    [Fact]
    public async Task RevokeOrExpireUnknownAndTerminalCapabilitiesAreRejected()
    {
        Fixture fixture = AuthorizedProjectFixture();
        CapabilityStateData terminal = IssuedState();
        terminal.Apply(new MagicLinkConfirmationCapabilityRevoked(
            CapabilityId(),
            Context().Tenant!,
            Context().Actor!,
            RevokedAtUtc(),
            new MagicLinkAuditMetadata("timesheets", "revoke-1")));

        MagicLinkCapabilityCommandResult unknownRevoke = await fixture.CreateService().RevokeAsync(
            Context(),
            RevokeCommand(),
            null,
            RevokedAtUtc(),
            TestContext.Current.CancellationToken);
        MagicLinkCapabilityCommandResult terminalExpire = await fixture.CreateService().ExpireAsync(
            Context(),
            ExpireCommand(),
            terminal,
            ExpiresAtUtc(),
            TestContext.Current.CancellationToken);

        unknownRevoke.DomainResult.ShouldNotBeNull().IsRejection.ShouldBeTrue();
        terminalExpire.DomainResult.ShouldNotBeNull().IsRejection.ShouldBeTrue();
    }

    [Fact]
    public async Task ConfirmMagicLinkConsumesCapabilityAndRecordsContributorEvidenceNotApproval()
    {
        Fixture fixture = AuthorizedProjectFixture();

        MagicLinkConfirmationUseResult result = await fixture.CreateService().ConfirmAsync(
            Context(),
            "opaque-once",
            ConfirmCommand(),
            IssuedState(),
            RecordedExternalState(),
            ConfirmedAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeTrue();
        MagicLinkConfirmationCapabilityUsed used = result.CapabilityResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<MagicLinkConfirmationCapabilityUsed>();
        used.CapabilityId.ShouldBe(CapabilityId());
        used.Contributor.ShouldBe(Contributor());
        used.TimeEntryId.ShouldBe(TimeEntryId());
        used.Source.ShouldBe(new MagicLinkAuditMetadata("magic-link", "capability-1"));

        TimeEntryConfirmationCommandResult timeEntryResult = result.TimeEntryResult.ShouldNotBeNull();
        TimesheetsDomainResult timeEntryDomainResult = timeEntryResult.DomainResult.ShouldNotBeNull();
        TimeEntryContributorConfirmed confirmed = timeEntryDomainResult
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimeEntryContributorConfirmed>();
        confirmed.Source.ShouldBe(new ExternalContributionSource("magic-link", "capability-1"));
        timeEntryDomainResult.Events.ShouldNotContain(static @event => @event is TimeEntryApproved);
    }

    [Fact]
    public async Task AdjustMagicLinkUpdatesDraftExternalValuesThenConsumesCapability()
    {
        Fixture fixture = AuthorizedProjectFixture();
        AdjustTimeThroughMagicLink command = AdjustCommand() with
        {
            Comment = new TimeEntryComment("adjustment private note",
                TimeEntryCommentPolicy.SensitiveDefault with
                { ProjectionInclusion = TimesheetsCommentPolicyDecision.Allowed })
        };

        MagicLinkConfirmationUseResult result = await fixture.CreateService().AdjustAsync(
            Context(),
            "opaque-once",
            command,
            IssuedState(allowedAction: MagicLinkAllowedAction.Adjust),
            RecordedExternalState(),
            FreshCatalog(),
            ConfirmedAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeTrue();
        TimeEntryAdjustedThroughMagicLink adjusted = result.AdjustmentResult.ShouldNotBeNull()
            .DomainResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimeEntryAdjustedThroughMagicLink>();
        adjusted.AdjustedValues.ServiceDate.ShouldBe(command.ServiceDate);
        adjusted.AdjustedValues.DurationMinutes.ShouldBe(command.DurationMinutes);
        adjusted.AdjustedValues.BillableState.ShouldBe(command.BillableState);
        adjusted.AdjustedValues.Target.ShouldBe(TimeEntryTargetReference.ForProject(Project()));
        adjusted.AdjustedValues.Contributor.ShouldBe(Contributor());
        adjusted.PreviousValues.ActivityTypeScope.ShouldBe(ActivityTypeScope.Tenant);
        adjusted.AdjustedValues.ActivityTypeScope.ShouldBe(ActivityTypeScope.Tenant);
        adjusted.ActivityTypeScope.ShouldBe(adjusted.AdjustedValues.ActivityTypeScope!.Value);
        adjusted.Source.ShouldBe(new ExternalContributionSource("magic-link", "capability-1"));
        adjusted.AdjustedValues.Comment.ShouldNotBeNull().Policy.ServerPublicationVersion
            .ShouldBe(TimeEntryCommentPublicationIssuer.CurrentVersion);
        adjusted.AdjustedValues.Comment.Policy.ServerProjectionInclusion
            .ShouldBe(TimesheetsCommentPolicyDecision.PolicyRequired);

        MagicLinkConfirmationCapabilityUsed used = result.CapabilityResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<MagicLinkConfirmationCapabilityUsed>();
        used.OutcomeCategory.ShouldBe("adjusted");
    }

    [Fact]
    public async Task ProjectOwnedActivityTypeIsRejectedAcrossIssueDisplayConfirmAndAdjust()
    {
        Fixture fixture = AuthorizedProjectFixture();
        Fixture workFixture = AuthorizedWorkFixture();
        MagicLinkConfirmationCapabilityCommandService service = fixture.CreateService();
        var projectCatalog = new ActivityTypeCatalogReadModel(
            [new ActivityTypeCatalogItem(
                ActivityId(),
                ActivityTypeScope.Project,
                Project(),
                "Project delivery",
                true,
                BillableState.Billable)],
            ProjectionFreshnessMetadata.Fresh);
        TimeEntryState projectScopedEntry = RecordedExternalState(activityTypeScope: ActivityTypeScope.Project);

        MagicLinkCapabilityCommandResult issuance = await workFixture.CreateService().IssueAsync(
            Context(),
            IssueWorkCommand(),
            null,
            projectCatalog,
            IssuedAtUtc(),
            TestContext.Current.CancellationToken);
        MagicLinkConfirmationDisplayResponse? display = await service.DescribeAsync(
            Context(),
            "opaque-once",
            IssuedState(),
            projectScopedEntry,
            projectCatalog,
            ConfirmedAtUtc(),
            TestContext.Current.CancellationToken);
        MagicLinkConfirmationUseResult confirmation = await service.ConfirmAsync(
            Context(),
            "opaque-once",
            ConfirmCommand(),
            IssuedState(),
            projectScopedEntry,
            ConfirmedAtUtc(),
            TestContext.Current.CancellationToken);
        MagicLinkConfirmationUseResult adjustment = await service.AdjustAsync(
            Context(),
            "opaque-once",
            AdjustCommand(),
            IssuedState(allowedAction: MagicLinkAllowedAction.Adjust),
            RecordedExternalState(),
            projectCatalog,
            ConfirmedAtUtc(),
            TestContext.Current.CancellationToken);

        TimesheetsRejection issuanceRejection = issuance.DomainResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimesheetsRejection>();
        issuanceRejection.Code.ShouldBe(TimesheetsRejectionCode.ActivityTypeScopeMismatch);
        issuanceRejection.FieldErrors.ShouldContain(static error =>
            error.Field == "activityTypeId" && error.Code == "scope-mismatch");
        issuance.IssueResponse.ShouldBeNull();
        workFixture.TokenGenerator.GenerateCount.ShouldBe(0);
        display.ShouldBeNull();
        confirmation.WasDispatched.ShouldBeFalse();
        confirmation.TimeEntryResult.ShouldBeNull();
        adjustment.WasDispatched.ShouldBeFalse();
        adjustment.AdjustmentResult.ShouldBeNull();
    }

    [Fact]
    public async Task TenantOwnedActivityTypeRemainsValidForWorkTargetMagicLinkPaths()
    {
        Fixture fixture = AuthorizedWorkFixture();
        MagicLinkConfirmationCapabilityCommandService service = fixture.CreateService();
        TimeEntryTargetReference workTarget = TimeEntryTargetReference.ForWork(new WorkReference("work-1"));
        TimeEntryState workEntry = RecordedExternalState(target: workTarget);

        MagicLinkCapabilityCommandResult issuance = await service.IssueAsync(
            Context(),
            IssueWorkCommand(),
            null,
            FreshCatalog(),
            IssuedAtUtc(),
            TestContext.Current.CancellationToken);
        MagicLinkConfirmationCapabilityIssued issued = issuance.DomainResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem().ShouldBeOfType<MagicLinkConfirmationCapabilityIssued>();
        MagicLinkConfirmationDisplayResponse? display = await service.DescribeAsync(
            Context(),
            "opaque-once",
            IssuedState(target: workTarget),
            workEntry,
            FreshCatalog(),
            ConfirmedAtUtc(),
            TestContext.Current.CancellationToken);
        MagicLinkConfirmationUseResult confirmation = await service.ConfirmAsync(
            Context(),
            "opaque-once",
            ConfirmCommand(),
            IssuedState(target: workTarget),
            workEntry,
            ConfirmedAtUtc(),
            TestContext.Current.CancellationToken);
        MagicLinkAdjustmentDisplayResponse? adjustmentDisplay = await service.DescribeAdjustmentAsync(
            Context(),
            "opaque-once",
            IssuedState(target: workTarget, allowedAction: MagicLinkAllowedAction.Adjust),
            workEntry,
            FreshCatalog(),
            ConfirmedAtUtc(),
            TestContext.Current.CancellationToken);
        MagicLinkConfirmationUseResult adjustment = await service.AdjustAsync(
            Context(),
            "opaque-once",
            AdjustCommand(),
            IssuedState(target: workTarget, allowedAction: MagicLinkAllowedAction.Adjust),
            workEntry,
            FreshCatalog(),
            ConfirmedAtUtc(),
            TestContext.Current.CancellationToken);

        issuance.WasDispatched.ShouldBeTrue();
        issuance.IssueResponse.ShouldNotBeNull();
        issued.Target.ShouldBe(workTarget);
        display.ShouldNotBeNull().TargetContext.ShouldBe(TimeEntryTargetKind.Work.ToString());
        confirmation.WasDispatched.ShouldBeTrue();
        confirmation.TimeEntryResult.ShouldNotBeNull().DomainResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem().ShouldBeOfType<TimeEntryContributorConfirmed>();
        adjustmentDisplay.ShouldNotBeNull().TargetContext.ShouldBe(TimeEntryTargetKind.Work.ToString());
        adjustment.WasDispatched.ShouldBeTrue();
        TimeEntryAdjustedThroughMagicLink adjusted = adjustment.AdjustmentResult.ShouldNotBeNull()
            .DomainResult.ShouldNotBeNull().Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimeEntryAdjustedThroughMagicLink>();
        adjusted.AdjustedValues.Target.ShouldBe(workTarget);
        adjusted.ActivityTypeScope.ShouldBe(ActivityTypeScope.Tenant);
    }

    [Fact]
    public async Task AdjustMagicLinkFailsClosedWithoutCapabilityUseWhenValuesAreInvalid()
    {
        Fixture fixture = AuthorizedProjectFixture();

        MagicLinkConfirmationUseResult result = await fixture.CreateService().AdjustAsync(
            Context(),
            "opaque-once",
            AdjustCommand() with { DurationMinutes = 0 },
            IssuedState(allowedAction: MagicLinkAllowedAction.Adjust),
            RecordedExternalState(),
            FreshCatalog(),
            ConfirmedAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeFalse();
        result.CapabilityResult.ShouldBeNull();
        result.AdjustmentResult.ShouldNotBeNull().DomainResult.ShouldNotBeNull().IsRejection.ShouldBeTrue();
    }

    [Fact]
    public async Task AdjustMagicLinkRejectsConfirmOnlyCapabilityWithoutTimeEntryAdjustment()
    {
        Fixture fixture = AuthorizedProjectFixture();

        MagicLinkConfirmationUseResult result = await fixture.CreateService().AdjustAsync(
            Context(),
            "opaque-once",
            AdjustCommand(),
            IssuedState(allowedAction: MagicLinkAllowedAction.Confirm),
            RecordedExternalState(),
            FreshCatalog(),
            ConfirmedAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeFalse();
        result.CapabilityResult.ShouldNotBeNull().IsRejection.ShouldBeTrue();
        result.AdjustmentResult.ShouldBeNull();
    }

    [Fact]
    public async Task ConfirmationAndAdjustmentInvalidLinkRejectionsHaveIdenticalNeutralMessages()
    {
        Fixture fixture = AuthorizedProjectFixture();

        MagicLinkConfirmationUseResult confirmation = await fixture.CreateService().ConfirmAsync(
            Context(),
            "different-token",
            ConfirmCommand(),
            IssuedState(),
            RecordedExternalState(),
            ConfirmedAtUtc(),
            TestContext.Current.CancellationToken);

        MagicLinkConfirmationUseResult adjustment = await fixture.CreateService().AdjustAsync(
            Context(),
            "different-token",
            AdjustCommand(),
            IssuedState(allowedAction: MagicLinkAllowedAction.Adjust),
            RecordedExternalState(),
            FreshCatalog(),
            ConfirmedAtUtc(),
            TestContext.Current.CancellationToken);

        string confirmationMessage = confirmation.CapabilityResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimesheetsRejection>()
            .Message;
        string adjustmentMessage = adjustment.CapabilityResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimesheetsRejection>()
            .Message;

        confirmationMessage.ShouldBe(MagicLinkInvalidLinkDenial.Default.Title);
        adjustmentMessage.ShouldBe(confirmationMessage);
        adjustment.AdjustmentResult.ShouldBeNull();
    }

    [Fact]
    public async Task InvalidLinkCategoriesAllProduceEquivalentExternalServiceResultsWithoutDispatch()
    {
        Fixture fixture = AuthorizedProjectFixture();
        CapabilityStateData used = IssuedState();
        used.Apply(new MagicLinkConfirmationCapabilityUsed(
            CapabilityId(),
            Context().Tenant!,
            Contributor(),
            TimeEntryId(),
            ConfirmedAtUtc(),
            new MagicLinkAuditMetadata("magic-link", "capability-1")));
        CapabilityStateData revoked = IssuedState();
        revoked.Apply(new MagicLinkConfirmationCapabilityRevoked(
            CapabilityId(),
            Context().Tenant!,
            Context().Actor!,
            RevokedAtUtc(),
            new MagicLinkAuditMetadata("timesheets", "revoke-1")));
        CapabilityStateData expired = IssuedState();
        expired.Apply(new MagicLinkConfirmationCapabilityExpired(
            CapabilityId(),
            Context().Tenant!,
            ExpiresAtUtc(),
            new MagicLinkAuditMetadata("timesheets", "expire-1")));

        (string Token, CapabilityStateData? Capability, TimeEntryState? TimeEntry, DateTimeOffset AtUtc)[] cases =
        [
            ("", IssuedState(), RecordedExternalState(), ConfirmedAtUtc()),
            ("different-token", IssuedState(), RecordedExternalState(), ConfirmedAtUtc()),
            ("opaque-once", null, RecordedExternalState(), ConfirmedAtUtc()),
            ("opaque-once", expired, RecordedExternalState(), ConfirmedAtUtc()),
            ("opaque-once", used, RecordedExternalState(), ConfirmedAtUtc()),
            ("opaque-once", revoked, RecordedExternalState(), ConfirmedAtUtc()),
            ("opaque-once", IssuedState(tenant: new TenantReference("tenant-2")), RecordedExternalState(), ConfirmedAtUtc()),
            ("opaque-once", IssuedState(contributor: new PartyReference("party-2")), RecordedExternalState(), ConfirmedAtUtc()),
            ("opaque-once", IssuedState(allowedAction: MagicLinkAllowedAction.Adjust), RecordedExternalState(), ConfirmedAtUtc()),
            ("opaque-once", IssuedState(targetKind: MagicLinkTargetKind.ExistingTimeEntry), RecordedExternalState(), ConfirmedAtUtc()),
            ("opaque-once", IssuedState(), RecordedExternalState(timeEntryId: new TimeEntryId("time-entry-2")), ConfirmedAtUtc()),
            ("opaque-once", IssuedState(), RecordedExternalState(target: TimeEntryTargetReference.ForProject(new ProjectReference("project-2"))), ConfirmedAtUtc()),
            ("opaque-once", IssuedState(), RecordedExternalState(contributor: new PartyReference("party-2")), ConfirmedAtUtc()),
            ("opaque-once", IssuedState(expiresAtUtc: ConfirmedAtUtc()), RecordedExternalState(), ConfirmedAtUtc())
        ];

        foreach ((string token, CapabilityStateData? capability, TimeEntryState? timeEntry, DateTimeOffset atUtc) in cases)
        {
            MagicLinkConfirmationUseResult result = await fixture.CreateService().ConfirmAsync(
                Context(),
                token,
                ConfirmCommand(),
                capability,
                timeEntry,
                atUtc,
                TestContext.Current.CancellationToken);

            result.WasDispatched.ShouldBeFalse();
            result.TimeEntryResult.ShouldBeNull();
            result.CapabilityResult.ShouldNotBeNull().IsRejection.ShouldBeTrue();
            result.CapabilityResult.Events.ShouldHaveSingleItem()
                .ShouldBeOfType<TimesheetsRejection>()
                .Message.ShouldBe(MagicLinkInvalidLinkDenial.Default.Title);
        }
    }

    [Fact]
    public async Task AdjustInvalidLinkCategoriesAllProduceEquivalentExternalServiceResultsWithoutDispatch()
    {
        Fixture fixture = AuthorizedProjectFixture();
        CapabilityStateData used = IssuedState(allowedAction: MagicLinkAllowedAction.Adjust);
        used.Apply(new MagicLinkConfirmationCapabilityUsed(
            CapabilityId(),
            Context().Tenant!,
            Contributor(),
            TimeEntryId(),
            ConfirmedAtUtc(),
            new MagicLinkAuditMetadata("magic-link", "capability-1")));
        CapabilityStateData revoked = IssuedState(allowedAction: MagicLinkAllowedAction.Adjust);
        revoked.Apply(new MagicLinkConfirmationCapabilityRevoked(
            CapabilityId(),
            Context().Tenant!,
            Context().Actor!,
            RevokedAtUtc(),
            new MagicLinkAuditMetadata("timesheets", "revoke-1")));
        CapabilityStateData expired = IssuedState(allowedAction: MagicLinkAllowedAction.Adjust);
        expired.Apply(new MagicLinkConfirmationCapabilityExpired(
            CapabilityId(),
            Context().Tenant!,
            ExpiresAtUtc(),
            new MagicLinkAuditMetadata("timesheets", "expire-1")));

        (string Token, CapabilityStateData? Capability, TimeEntryState? TimeEntry, ActivityTypeCatalogReadModel Catalog, DateTimeOffset AtUtc)[] cases =
        [
            ("", IssuedState(allowedAction: MagicLinkAllowedAction.Adjust), RecordedExternalState(), FreshCatalog(), ConfirmedAtUtc()),
            ("malformed-token", IssuedState(allowedAction: MagicLinkAllowedAction.Adjust), RecordedExternalState(), FreshCatalog(), ConfirmedAtUtc()),
            ("different-token", IssuedState(allowedAction: MagicLinkAllowedAction.Adjust), RecordedExternalState(), FreshCatalog(), ConfirmedAtUtc()),
            ("opaque-once", null, RecordedExternalState(), FreshCatalog(), ConfirmedAtUtc()),
            ("opaque-once", expired, RecordedExternalState(), FreshCatalog(), ConfirmedAtUtc()),
            ("opaque-once", used, RecordedExternalState(), FreshCatalog(), ConfirmedAtUtc()),
            ("opaque-once", revoked, RecordedExternalState(), FreshCatalog(), ConfirmedAtUtc()),
            ("opaque-once", IssuedState(tenant: new TenantReference("tenant-2"), allowedAction: MagicLinkAllowedAction.Adjust), RecordedExternalState(), FreshCatalog(), ConfirmedAtUtc()),
            ("opaque-once", IssuedState(contributor: new PartyReference("party-2"), allowedAction: MagicLinkAllowedAction.Adjust), RecordedExternalState(), FreshCatalog(), ConfirmedAtUtc()),
            ("opaque-once", IssuedState(allowedAction: MagicLinkAllowedAction.Confirm), RecordedExternalState(), FreshCatalog(), ConfirmedAtUtc()),
            ("opaque-once", IssuedState(targetKind: MagicLinkTargetKind.ExistingTimeEntry, allowedAction: MagicLinkAllowedAction.Adjust), RecordedExternalState(), FreshCatalog(), ConfirmedAtUtc()),
            ("opaque-once", IssuedState(allowedAction: MagicLinkAllowedAction.Adjust), RecordedExternalState(timeEntryId: new TimeEntryId("time-entry-2")), FreshCatalog(), ConfirmedAtUtc()),
            ("opaque-once", IssuedState(allowedAction: MagicLinkAllowedAction.Adjust), RecordedExternalState(target: TimeEntryTargetReference.ForProject(new ProjectReference("project-2"))), FreshCatalog(), ConfirmedAtUtc()),
            ("opaque-once", IssuedState(allowedAction: MagicLinkAllowedAction.Adjust), RecordedExternalState(contributor: new PartyReference("party-2")), FreshCatalog(), ConfirmedAtUtc()),
            ("opaque-once", IssuedState(allowedAction: MagicLinkAllowedAction.Adjust), RecordedExternalState(), new ActivityTypeCatalogReadModel([], ProjectionFreshnessMetadata.Stale()), ConfirmedAtUtc()),
            ("opaque-once", IssuedState(allowedAction: MagicLinkAllowedAction.Adjust, expiresAtUtc: ConfirmedAtUtc()), RecordedExternalState(), FreshCatalog(), ConfirmedAtUtc())
        ];

        foreach ((string token, CapabilityStateData? capability, TimeEntryState? timeEntry, ActivityTypeCatalogReadModel catalog, DateTimeOffset atUtc) in cases)
        {
            MagicLinkConfirmationUseResult result = await fixture.CreateService().AdjustAsync(
                Context(),
                token,
                AdjustCommand(),
                capability,
                timeEntry,
                catalog,
                atUtc,
                TestContext.Current.CancellationToken);

            result.WasDispatched.ShouldBeFalse();
            result.AdjustmentResult.ShouldBeNull();
            result.CapabilityResult.ShouldNotBeNull().IsRejection.ShouldBeTrue();
            result.CapabilityResult.Events.ShouldHaveSingleItem()
                .ShouldBeOfType<TimesheetsRejection>()
                .Message.ShouldBe(MagicLinkInvalidLinkDenial.Default.Title);
        }
    }

    [Fact]
    public async Task MalformedMagicLinkTokensAreNeutralForDescribeConfirmAndAdjust()
    {
        Fixture fixture = AuthorizedProjectFixture();

        MagicLinkConfirmationDisplayResponse? confirmationDisplay = await fixture.CreateService().DescribeAsync(
            Context(),
            "malformed-token",
            IssuedState(),
            RecordedExternalState(),
            FreshCatalog(),
            ConfirmedAtUtc(),
            TestContext.Current.CancellationToken);
        MagicLinkConfirmationUseResult confirmation = await fixture.CreateService().ConfirmAsync(
            Context(),
            "malformed-token",
            ConfirmCommand(),
            IssuedState(),
            RecordedExternalState(),
            ConfirmedAtUtc(),
            TestContext.Current.CancellationToken);
        MagicLinkAdjustmentDisplayResponse? adjustmentDisplay = await fixture.CreateService().DescribeAdjustmentAsync(
            Context(),
            "malformed-token",
            IssuedState(allowedAction: MagicLinkAllowedAction.Adjust),
            RecordedExternalState(),
            FreshCatalog(),
            ConfirmedAtUtc(),
            TestContext.Current.CancellationToken);
        MagicLinkConfirmationUseResult adjustment = await fixture.CreateService().AdjustAsync(
            Context(),
            "malformed-token",
            AdjustCommand(),
            IssuedState(allowedAction: MagicLinkAllowedAction.Adjust),
            RecordedExternalState(),
            FreshCatalog(),
            ConfirmedAtUtc(),
            TestContext.Current.CancellationToken);

        confirmationDisplay.ShouldBeNull();
        adjustmentDisplay.ShouldBeNull();
        confirmation.WasDispatched.ShouldBeFalse();
        confirmation.TimeEntryResult.ShouldBeNull();
        confirmation.CapabilityResult.ShouldNotBeNull().Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimesheetsRejection>()
            .Message.ShouldBe(MagicLinkInvalidLinkDenial.Default.Title);
        adjustment.WasDispatched.ShouldBeFalse();
        adjustment.AdjustmentResult.ShouldBeNull();
        adjustment.CapabilityResult.ShouldNotBeNull().Events.ShouldHaveSingleItem()
            .ShouldBeOfType<TimesheetsRejection>()
            .Message.ShouldBe(MagicLinkInvalidLinkDenial.Default.Title);
    }

    [Fact]
    public async Task UnauthorizedMagicLinkDisclosurePathsReturnNoDisplayOrDispatchResult()
    {
        Fixture fixture = new();
        fixture.TenantValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), TimesheetsOperation.MagicLinkDisclosure, Arg.Any<CancellationToken>())
            .Returns(TimesheetsTenantAccessResult.Denied(
                TimesheetsTenantAccessState.MissingTenant,
                "Authority cannot be resolved."));

        MagicLinkConfirmationDisplayResponse? confirmationDisplay = await fixture.CreateService().DescribeAsync(
            Context(),
            "opaque-once",
            IssuedState(),
            RecordedExternalState(),
            FreshCatalog(),
            ConfirmedAtUtc(),
            TestContext.Current.CancellationToken);
        MagicLinkConfirmationUseResult confirmation = await fixture.CreateService().ConfirmAsync(
            Context(),
            "opaque-once",
            ConfirmCommand(),
            IssuedState(),
            RecordedExternalState(),
            ConfirmedAtUtc(),
            TestContext.Current.CancellationToken);
        MagicLinkAdjustmentDisplayResponse? adjustmentDisplay = await fixture.CreateService().DescribeAdjustmentAsync(
            Context(),
            "opaque-once",
            IssuedState(allowedAction: MagicLinkAllowedAction.Adjust),
            RecordedExternalState(),
            FreshCatalog(),
            ConfirmedAtUtc(),
            TestContext.Current.CancellationToken);
        MagicLinkConfirmationUseResult adjustment = await fixture.CreateService().AdjustAsync(
            Context(),
            "opaque-once",
            AdjustCommand(),
            IssuedState(allowedAction: MagicLinkAllowedAction.Adjust),
            RecordedExternalState(),
            FreshCatalog(),
            ConfirmedAtUtc(),
            TestContext.Current.CancellationToken);

        confirmationDisplay.ShouldBeNull();
        adjustmentDisplay.ShouldBeNull();
        confirmation.WasDispatched.ShouldBeFalse();
        confirmation.CapabilityResult.ShouldBeNull();
        confirmation.TimeEntryResult.ShouldBeNull();
        adjustment.WasDispatched.ShouldBeFalse();
        adjustment.CapabilityResult.ShouldBeNull();
        adjustment.AdjustmentResult.ShouldBeNull();
    }

    [Theory]
    [InlineData("different-token")]
    [InlineData("")]
    public async Task ConfirmMagicLinkFailsClosedForInvalidTokensWithoutTimeEntryConfirmation(string token)
    {
        Fixture fixture = AuthorizedProjectFixture();

        MagicLinkConfirmationUseResult result = await fixture.CreateService().ConfirmAsync(
            Context(),
            token,
            ConfirmCommand(),
            IssuedState(),
            RecordedExternalState(),
            ConfirmedAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeFalse();
        result.CapabilityResult.ShouldNotBeNull().IsRejection.ShouldBeTrue();
        result.TimeEntryResult.ShouldBeNull();
    }

    [Fact]
    public async Task ConfirmMagicLinkRejectsReuseWithoutTimeEntryConfirmation()
    {
        Fixture fixture = AuthorizedProjectFixture();
        CapabilityStateData state = IssuedState();
        state.Apply(new MagicLinkConfirmationCapabilityUsed(
            CapabilityId(),
            Context().Tenant!,
            Contributor(),
            TimeEntryId(),
            ConfirmedAtUtc(),
            new MagicLinkAuditMetadata("magic-link", "capability-1")));

        MagicLinkConfirmationUseResult result = await fixture.CreateService().ConfirmAsync(
            Context(),
            "opaque-once",
            ConfirmCommand(),
            state,
            RecordedExternalState(),
            ConfirmedAtUtc(),
            TestContext.Current.CancellationToken);

        result.WasDispatched.ShouldBeFalse();
        result.CapabilityResult.ShouldNotBeNull().IsRejection.ShouldBeTrue();
        result.TimeEntryResult.ShouldBeNull();
    }

    [Fact]
    public async Task ConfirmMagicLinkFailsClosedForUnavailableOrTerminalCapabilitiesWithoutTimeEntryConfirmation()
    {
        Fixture fixture = AuthorizedProjectFixture();
        CapabilityStateData revoked = IssuedState();
        revoked.Apply(new MagicLinkConfirmationCapabilityRevoked(
            CapabilityId(),
            Context().Tenant!,
            Context().Actor!,
            RevokedAtUtc(),
            new MagicLinkAuditMetadata("timesheets", "revoke-1")));
        CapabilityStateData expired = IssuedState();
        expired.Apply(new MagicLinkConfirmationCapabilityExpired(
            CapabilityId(),
            Context().Tenant!,
            ExpiresAtUtc(),
            new MagicLinkAuditMetadata("timesheets", "expire-1")));

        foreach (CapabilityStateData? state in new CapabilityStateData?[] { null, revoked, expired })
        {
            MagicLinkConfirmationUseResult result = await fixture.CreateService().ConfirmAsync(
                Context(),
                "opaque-once",
                ConfirmCommand(),
                state,
                RecordedExternalState(),
                ConfirmedAtUtc(),
                TestContext.Current.CancellationToken);

            result.WasDispatched.ShouldBeFalse();
            result.CapabilityResult.ShouldNotBeNull().IsRejection.ShouldBeTrue();
            result.TimeEntryResult.ShouldBeNull();
        }
    }

    [Fact]
    public async Task ConfirmMagicLinkFailsClosedForExpiredTenantMismatchedWrongActionOrWrongScopeState()
    {
        Fixture fixture = AuthorizedProjectFixture();
        (CapabilityStateData State, DateTimeOffset ConfirmedAtUtc)[] cases =
        [
            (IssuedState(expiresAtUtc: ConfirmedAtUtc()), ConfirmedAtUtc()),
            (IssuedState(tenant: new TenantReference("tenant-2")), ConfirmedAtUtc()),
            (IssuedState(allowedAction: MagicLinkAllowedAction.Adjust), ConfirmedAtUtc()),
            (IssuedState(targetKind: MagicLinkTargetKind.ExistingTimeEntry), ConfirmedAtUtc())
        ];

        foreach ((CapabilityStateData state, DateTimeOffset confirmedAtUtc) in cases)
        {
            MagicLinkConfirmationUseResult result = await fixture.CreateService().ConfirmAsync(
                Context(),
                "opaque-once",
                ConfirmCommand(),
                state,
                RecordedExternalState(),
                confirmedAtUtc,
                TestContext.Current.CancellationToken);

            result.WasDispatched.ShouldBeFalse();
            result.CapabilityResult.ShouldNotBeNull().IsRejection.ShouldBeTrue();
            result.TimeEntryResult.ShouldBeNull();
        }
    }

    [Fact]
    public async Task ConfirmMagicLinkFailsClosedWhenTimeEntryStateDoesNotMatchCapabilityScope()
    {
        Fixture fixture = AuthorizedProjectFixture();
        TimeEntryState differentContributor = RecordedExternalState(contributor: new PartyReference("party-2"));
        TimeEntryState differentTarget = RecordedExternalState(target: TimeEntryTargetReference.ForProject(new ProjectReference("project-2")));
        TimeEntryState differentEntry = RecordedExternalState(timeEntryId: new TimeEntryId("time-entry-2"));

        foreach (TimeEntryState state in new[] { differentContributor, differentTarget, differentEntry })
        {
            MagicLinkConfirmationUseResult result = await fixture.CreateService().ConfirmAsync(
                Context(),
                "opaque-once",
                ConfirmCommand(),
                IssuedState(),
                state,
                ConfirmedAtUtc(),
                TestContext.Current.CancellationToken);

            result.WasDispatched.ShouldBeFalse();
            result.CapabilityResult.ShouldNotBeNull().IsRejection.ShouldBeTrue();
            result.TimeEntryResult.ShouldBeNull();
        }
    }

    [Fact]
    public async Task DescribeMagicLinkReturnsOnlySafeConfirmationDetailsForValidToken()
    {
        Fixture fixture = AuthorizedProjectFixture();

        MagicLinkConfirmationDisplayResponse response = (await fixture.CreateService().DescribeAsync(
            Context(),
            "opaque-once",
            IssuedState(),
            RecordedExternalState(),
            FreshCatalog(),
            ConfirmedAtUtc(),
            TestContext.Current.CancellationToken)).ShouldNotBeNull();

        response.ProposedDate.ShouldBe(new DateOnly(2026, 6, 19));
        response.DurationMinutes.ShouldBe(60);
        response.DurationUnit.ShouldBe("minutes");
        response.ActivityTypeId.ShouldBe(ActivityId());
        response.ActivityTypeLabel.ShouldBe("Delivery");
        response.BillableState.ShouldBe(BillableState.Billable);
        response.TargetContext.ShouldBe("Project");
        response.Comment.ShouldBeNull();

        string json = System.Text.Json.JsonSerializer.Serialize(
            response,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        json.ShouldNotContain("opaque-once");
        json.ShouldNotContain("token", Case.Insensitive);
        json.ShouldNotContain("party", Case.Insensitive);
        json.ShouldNotContain("tenant", Case.Insensitive);
    }

    /// <summary>Preserves confirmation of recorded evidence when capture availability changes.</summary>
    /// <param name="isActive">Whether the recorded Activity Type is active.</param>
    /// <param name="isAvailableForCapture">Whether the type permits new capture.</param>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task RecordedActivityTypeRemainsConfirmableWhenUnavailableForCapture(bool isActive, bool isAvailableForCapture)
    {
        Fixture fixture = AuthorizedProjectFixture();
        MagicLinkConfirmationCapabilityCommandService service = fixture.CreateService();
        ActivityTypeCatalogReadModel catalog = FreshCatalog() with
        {
            Items = [FreshCatalog().Items[0] with
            {
                IsActive = isActive,
                IsAvailableForCapture = isAvailableForCapture
            }]
        };

        MagicLinkConfirmationDisplayResponse? display = await service.DescribeAsync(
            Context(), "opaque-once", IssuedState(), RecordedExternalState(), catalog,
            ConfirmedAtUtc(), TestContext.Current.CancellationToken);
        MagicLinkConfirmationUseResult confirmation = await service.ConfirmAsync(
            Context(), "opaque-once", ConfirmCommand(), IssuedState(), RecordedExternalState(),
            ConfirmedAtUtc(), TestContext.Current.CancellationToken);
        MagicLinkAdjustmentDisplayResponse? adjustmentDisplay = await service.DescribeAdjustmentAsync(
            Context(), "opaque-once", IssuedState(allowedAction: MagicLinkAllowedAction.Adjust),
            RecordedExternalState(), catalog, ConfirmedAtUtc(), TestContext.Current.CancellationToken);
        MagicLinkConfirmationUseResult adjustment = await service.AdjustAsync(
            Context(), "opaque-once", AdjustCommand(), IssuedState(allowedAction: MagicLinkAllowedAction.Adjust),
            RecordedExternalState(), catalog, ConfirmedAtUtc(), TestContext.Current.CancellationToken);
        MagicLinkCapabilityCommandResult issuance = await service.IssueAsync(
            Context(), IssueCommand(), null, catalog, IssuedAtUtc(), TestContext.Current.CancellationToken);

        display.ShouldNotBeNull().ActivityTypeLabel.ShouldBe("Delivery");
        confirmation.WasDispatched.ShouldBeTrue();
        adjustmentDisplay.ShouldBeNull();
        adjustment.WasDispatched.ShouldBeFalse();
        adjustment.AdjustmentResult.ShouldBeNull();
        issuance.DomainResult.ShouldNotBeNull().IsRejection.ShouldBeTrue();
        issuance.IssueResponse.ShouldBeNull();
    }

    [Fact]
    public async Task AdjustmentDisplayRequiresServerIssuedCommentPermission()
    {
        Fixture fixture = AuthorizedProjectFixture();
        TimeEntryComment callerAllowed = new("private note", TimeEntryCommentPolicy.SensitiveDefault with
        {
            ExternalConfirmationDisplay = TimesheetsCommentPolicyDecision.Allowed
        });
        MagicLinkConfirmationCapabilityCommandService service = fixture.CreateService();

        MagicLinkAdjustmentDisplayResponse legacy = (await service.DescribeAdjustmentAsync(
            Context(), "opaque-once", IssuedState(allowedAction: MagicLinkAllowedAction.Adjust),
            RecordedExternalState(comment: callerAllowed), FreshCatalog(), ConfirmedAtUtc(),
            TestContext.Current.CancellationToken)).ShouldNotBeNull();
        legacy.Comment.ShouldBeNull();

        TimeEntryComment olderIssued = new("older private note", TimeEntryCommentPolicy.SensitiveDefault with
        {
            ServerPublicationVersion = TimeEntryCommentPublicationIssuer.CurrentVersion,
            ExternalConfirmationDisplay = TimesheetsCommentPolicyDecision.Allowed
        });
        MagicLinkAdjustmentDisplayResponse olderDisplay = (await service.DescribeAdjustmentAsync(
            Context(), "opaque-once", IssuedState(allowedAction: MagicLinkAllowedAction.Adjust),
            RecordedExternalState(comment: olderIssued), FreshCatalog(), ConfirmedAtUtc(),
            TestContext.Current.CancellationToken)).ShouldNotBeNull();
        olderDisplay.Comment.ShouldBeNull();

        TimeEntryComment formerlyAllowed = new("old caller-cleared note", TimeEntryCommentPolicy.SensitiveDefault with
        {
            ServerPublicationVersion = 1,
            ExternalConfirmationDisplay = TimesheetsCommentPolicyDecision.Allowed,
            ServerExternalConfirmationDisplay = TimesheetsCommentPolicyDecision.Allowed,
            RedactionRequirement = TimesheetsCommentRedactionRequirement.NotRequired
        });
        MagicLinkAdjustmentDisplayResponse historicalDisplay = (await service.DescribeAdjustmentAsync(
            Context(), "opaque-once", IssuedState(allowedAction: MagicLinkAllowedAction.Adjust),
            RecordedExternalState(comment: formerlyAllowed), FreshCatalog(), ConfirmedAtUtc(),
            TestContext.Current.CancellationToken)).ShouldNotBeNull();
        historicalDisplay.Comment.ShouldBeNull();

        TimeEntryCommentPublicationIssuer issuer = new();
        TimeEntryComment denied = issuer.Issue(Context(), callerAllowed).ShouldNotBeNull();
        MagicLinkAdjustmentDisplayResponse deniedDisplay = (await service.DescribeAdjustmentAsync(
            Context(), "opaque-once", IssuedState(allowedAction: MagicLinkAllowedAction.Adjust),
            RecordedExternalState(comment: denied), FreshCatalog(), ConfirmedAtUtc(),
            TestContext.Current.CancellationToken)).ShouldNotBeNull();
        deniedDisplay.Comment.ShouldBeNull();

        ITimeEntryCommentPublicationAuthority authority = Substitute.For<ITimeEntryCommentPublicationAuthority>();
        authority.MayDisplayExternally(Arg.Any<TimesheetsRequestContext>(), Arg.Any<TimeEntryComment>())
            .Returns(true);
        TimeEntryComment permitted = new TimeEntryCommentPublicationIssuer(authority)
            .Issue(Context(), callerAllowed).ShouldNotBeNull();
        permitted.Policy.ServerExternalConfirmationDisplay.ShouldBe(TimesheetsCommentPolicyDecision.Excluded);
        MagicLinkAdjustmentDisplayResponse allowedDisplay = (await service.DescribeAdjustmentAsync(
            Context(), "opaque-once", IssuedState(allowedAction: MagicLinkAllowedAction.Adjust),
            RecordedExternalState(comment: permitted), FreshCatalog(), ConfirmedAtUtc(),
            TestContext.Current.CancellationToken)).ShouldNotBeNull();
        allowedDisplay.Comment.ShouldBeNull();

        authority.MayDisplayUnredactedExternally(Arg.Any<TimesheetsRequestContext>(), Arg.Any<TimeEntryComment>())
            .Returns(true);
        TimeEntryComment cleared = new TimeEntryCommentPublicationIssuer(authority)
            .Issue(Context(), callerAllowed).ShouldNotBeNull();
        cleared.Policy.RedactionRequirement.ShouldBe(TimesheetsCommentRedactionRequirement.NotRequired);
        MagicLinkAdjustmentDisplayResponse clearedDisplay = (await service.DescribeAdjustmentAsync(
            Context(), "opaque-once", IssuedState(allowedAction: MagicLinkAllowedAction.Adjust),
            RecordedExternalState(comment: cleared), FreshCatalog(), ConfirmedAtUtc(),
            TestContext.Current.CancellationToken)).ShouldNotBeNull();
        clearedDisplay.Comment.ShouldBe("private note");
    }

    [Fact]
    public async Task DescribeMagicLinkFailsClosedForInvalidUsedExpiredWrongScopeOrUnfreshStates()
    {
        Fixture fixture = AuthorizedProjectFixture();
        CapabilityStateData used = IssuedState();
        used.Apply(new MagicLinkConfirmationCapabilityUsed(
            CapabilityId(),
            Context().Tenant!,
            Contributor(),
            TimeEntryId(),
            ConfirmedAtUtc(),
            new MagicLinkAuditMetadata("magic-link", "capability-1")));

        // (token, capability, time entry, catalog, observedAt) tuples that must each fail closed to null.
        (string Token, CapabilityStateData? Capability, TimeEntryState? TimeEntry, ActivityTypeCatalogReadModel Catalog, DateTimeOffset ObservedAt)[] cases =
        [
            ("different-token", IssuedState(), RecordedExternalState(), FreshCatalog(), ConfirmedAtUtc()),
            ("opaque-once", null, RecordedExternalState(), FreshCatalog(), ConfirmedAtUtc()),
            ("opaque-once", used, RecordedExternalState(), FreshCatalog(), ConfirmedAtUtc()),
            ("opaque-once", IssuedState(), RecordedExternalState(), FreshCatalog(), ExpiresAtUtc()),
            ("opaque-once", IssuedState(), RecordedExternalState(timeEntryId: new TimeEntryId("time-entry-2")), FreshCatalog(), ConfirmedAtUtc()),
            ("opaque-once", IssuedState(), null, FreshCatalog(), ConfirmedAtUtc()),
            ("opaque-once", IssuedState(), RecordedExternalState(), new ActivityTypeCatalogReadModel([], ProjectionFreshnessMetadata.Stale()), ConfirmedAtUtc())
        ];

        foreach ((string token, CapabilityStateData? capability, TimeEntryState? timeEntry, ActivityTypeCatalogReadModel catalog, DateTimeOffset observedAt) in cases)
        {
            MagicLinkConfirmationDisplayResponse? response = await fixture.CreateService().DescribeAsync(
                Context(),
                token,
                capability,
                timeEntry,
                catalog,
                observedAt,
                TestContext.Current.CancellationToken);

            response.ShouldBeNull();
        }
    }

    [Fact]
    public async Task DescribeAdjustmentFailsClosedForInvalidUsedExpiredWrongScopeOrUnfreshStates()
    {
        Fixture fixture = AuthorizedProjectFixture();
        CapabilityStateData used = IssuedState(allowedAction: MagicLinkAllowedAction.Adjust);
        used.Apply(new MagicLinkConfirmationCapabilityUsed(
            CapabilityId(),
            Context().Tenant!,
            Contributor(),
            TimeEntryId(),
            ConfirmedAtUtc(),
            new MagicLinkAuditMetadata("magic-link", "capability-1")));
        CapabilityStateData revoked = IssuedState(allowedAction: MagicLinkAllowedAction.Adjust);
        revoked.Apply(new MagicLinkConfirmationCapabilityRevoked(
            CapabilityId(),
            Context().Tenant!,
            Context().Actor!,
            RevokedAtUtc(),
            new MagicLinkAuditMetadata("timesheets", "revoke-1")));

        (string Token, CapabilityStateData? Capability, TimeEntryState? TimeEntry, ActivityTypeCatalogReadModel Catalog, DateTimeOffset ObservedAt)[] cases =
        [
            ("malformed-token", IssuedState(allowedAction: MagicLinkAllowedAction.Adjust), RecordedExternalState(), FreshCatalog(), ConfirmedAtUtc()),
            ("different-token", IssuedState(allowedAction: MagicLinkAllowedAction.Adjust), RecordedExternalState(), FreshCatalog(), ConfirmedAtUtc()),
            ("opaque-once", null, RecordedExternalState(), FreshCatalog(), ConfirmedAtUtc()),
            ("opaque-once", used, RecordedExternalState(), FreshCatalog(), ConfirmedAtUtc()),
            ("opaque-once", revoked, RecordedExternalState(), FreshCatalog(), ConfirmedAtUtc()),
            ("opaque-once", IssuedState(allowedAction: MagicLinkAllowedAction.Adjust), RecordedExternalState(), FreshCatalog(), ExpiresAtUtc()),
            ("opaque-once", IssuedState(allowedAction: MagicLinkAllowedAction.Adjust), RecordedExternalState(timeEntryId: new TimeEntryId("time-entry-2")), FreshCatalog(), ConfirmedAtUtc()),
            ("opaque-once", IssuedState(allowedAction: MagicLinkAllowedAction.Adjust), null, FreshCatalog(), ConfirmedAtUtc()),
            ("opaque-once", IssuedState(allowedAction: MagicLinkAllowedAction.Adjust), RecordedExternalState(), new ActivityTypeCatalogReadModel([], ProjectionFreshnessMetadata.Stale()), ConfirmedAtUtc())
        ];

        foreach ((string token, CapabilityStateData? capability, TimeEntryState? timeEntry, ActivityTypeCatalogReadModel catalog, DateTimeOffset observedAt) in cases)
        {
            MagicLinkAdjustmentDisplayResponse? response = await fixture.CreateService().DescribeAdjustmentAsync(
                Context(),
                token,
                capability,
                timeEntry,
                catalog,
                observedAt,
                TestContext.Current.CancellationToken);

            response.ShouldBeNull();
        }
    }

    [Fact]
    public async Task UnavailableMagicLinkStateLoaderReturnsOnlyFailClosedState()
    {
        UnavailableMagicLinkConfirmationCapabilityStateLoader loader = new();

        ActivityTypeCatalogReadModel catalog = await loader.LoadActivityTypeCatalogAsync(TestContext.Current.CancellationToken);
        CapabilityStateData? capability = await loader.LoadCapabilityAsync(CapabilityId(), TestContext.Current.CancellationToken);
        MagicLinkEndpointTokenState tokenState = await loader.LoadTokenStateAsync("opaque-once", TestContext.Current.CancellationToken);

        catalog.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Unavailable);
        capability.ShouldBeNull();
        tokenState.CapabilityState.ShouldBeNull();
        tokenState.TimeEntryState.ShouldBeNull();
        tokenState.ActivityTypeCatalog.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Unavailable);
    }

    [Fact]
    public void InvalidLinkOutcomeVocabularyIsInternalAndCorrelationSafe()
    {
        Enum.GetNames<MagicLinkInvalidLinkOutcomeCategory>().ShouldBe([
            "Unknown",
            "Malformed",
            "UnknownCapability",
            "HashMismatch",
            "Expired",
            "Revoked",
            "Used",
            "TenantMismatch",
            "WrongRecipient",
            "WrongAction",
            "WrongScope",
            "StaleCatalog",
            "Unauthorized",
            "RateLimited",
            "RepeatedAttempt"
        ]);

        // The outcome vocabulary must stay internal to the server diagnostics surface and must never
        // ship in the external contracts/OpenAPI surface that reaches magic-link recipients.
        typeof(MagicLinkInvalidLinkOutcomeCategory).Assembly
            .ShouldNotBe(typeof(MagicLinkInvalidLinkDenial).Assembly);
        typeof(MagicLinkInvalidLinkOutcomeCategory).Namespace
            .ShouldBe("Hexalith.Timesheets.Server.MagicLinks");
    }

    private static bool JsonContainsNoRawToken(MagicLinkConfirmationCapabilityIssued issued)
    {
        string json = System.Text.Json.JsonSerializer.Serialize(issued, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        json.ShouldNotContain("opaque-once");
        json.ShouldNotContain("oneTimeToken");
        json.ShouldNotContain("rawToken");
        return true;
    }

    private static IssueMagicLinkConfirmationCapability IssueCommand()
        => new(
            CapabilityId(),
            new MagicLinkConfirmationScope(
                Contributor(),
                TimeEntryTargetReference.ForProject(Project()),
                ActivityId(),
                TimeEntryId(),
                MagicLinkTargetKind.ProposedTimeEntry),
            MagicLinkAllowedAction.Confirm,
            ExpiresAtUtc(),
            new MagicLinkAuditMetadata("timesheets", "issue-1"));

    private static IssueMagicLinkConfirmationCapability IssueWorkCommand()
        => IssueCommand() with
        {
            Scope = IssueCommand().Scope with
            {
                Target = TimeEntryTargetReference.ForWork(new WorkReference("work-1"))
            }
        };

    private static RevokeMagicLinkConfirmationCapability RevokeCommand()
        => new(CapabilityId(), new MagicLinkAuditMetadata("timesheets", "revoke-1"));

    private static ExpireMagicLinkConfirmationCapability ExpireCommand()
        => new(CapabilityId(), new MagicLinkAuditMetadata("timesheets", "expire-1"));

    private static ConfirmTimeThroughMagicLink ConfirmCommand()
        => new();

    private static AdjustTimeThroughMagicLink AdjustCommand()
        => new(
            new DateOnly(2026, 6, 20),
            75,
            ActivityId(),
            BillableState.NonBillable);

    private static CapabilityStateData IssuedState(
        TenantReference? tenant = null,
        PartyReference? contributor = null,
        TimeEntryTargetReference? target = null,
        TimeEntryId? timeEntryId = null,
        MagicLinkTargetKind targetKind = MagicLinkTargetKind.ProposedTimeEntry,
        MagicLinkAllowedAction allowedAction = MagicLinkAllowedAction.Confirm,
        MagicLinkTokenHash? tokenHash = null,
        DateTimeOffset? expiresAtUtc = null)
    {
        CapabilityStateData state = new();
        state.Apply(new MagicLinkConfirmationCapabilityIssued(
            CapabilityId(),
            tenant ?? Context().Tenant!,
            contributor ?? Contributor(),
            target ?? TimeEntryTargetReference.ForProject(Project()),
            ActivityId(),
            timeEntryId ?? TimeEntryId(),
            targetKind,
            allowedAction,
            tokenHash ?? new MagicLinkTokenHash("hash-only"),
            expiresAtUtc ?? ExpiresAtUtc(),
            Context().Actor!,
            IssuedAtUtc(),
            new MagicLinkAuditMetadata("timesheets", "issue-1"),
            true));
        return state;
    }

    private static TimeEntryState RecordedExternalState(
        TimeEntryId? timeEntryId = null,
        TimeEntryTargetReference? target = null,
        PartyReference? contributor = null,
        ActivityTypeScope activityTypeScope = ActivityTypeScope.Tenant,
        TimeEntryComment? comment = null)
    {
        TimeEntryState state = new();
        state.Apply(new TimeEntryRecorded(
            timeEntryId ?? TimeEntryId(),
            target ?? TimeEntryTargetReference.ForProject(Project()),
            contributor ?? Contributor(),
            ActivityId(),
            activityTypeScope,
            new DateOnly(2026, 6, 19),
            60,
            BillableState.Billable,
            TimeEntryApprovalState.Draft,
            ContributorCategory.ExternalContributor,
            null)
        {
            Comment = comment,
            ExternalSource = new ExternalContributionSource("supplier-api", "request-1")
        });
        return state;
    }

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
        => new(new TenantReference("tenant-1"), new PartyReference("operator-1"), "correlation-1");

    private static DateTimeOffset IssuedAtUtc() => new(2026, 6, 19, 12, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset ExpiresAtUtc() => new(2026, 6, 20, 12, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset RevokedAtUtc() => new(2026, 6, 19, 13, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset ConfirmedAtUtc() => new(2026, 6, 19, 14, 0, 0, TimeSpan.Zero);

    private static MagicLinkCapabilityId CapabilityId() => new("capability-1");

    private static ProjectReference Project() => new("project-1");

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

    private static Fixture AuthorizedWorkFixture()
    {
        Fixture fixture = AuthorizedFixture();
        fixture.WorkValidator
            .ValidateAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<WorkReference>(), Arg.Any<CancellationToken>())
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

        public DeterministicTokenGenerator TokenGenerator { get; } = new();

        public MagicLinkConfirmationCapabilityCommandService CreateService()
        {
            TimesheetsAccessGuard accessGuard = new(
                TenantValidator,
                ProjectValidator,
                WorkValidator,
                PartyValidator,
                PolicyEvaluator);

            TimeEntryCommandService recordService = new(accessGuard);
            TimeEntrySubmissionCommandService submissionService = new(accessGuard);
            ExternalContributionCommandService externalContributionService = new(
                recordService,
                submissionService,
                accessGuard,
                new ExternalContributionPolicyOptions());

            return new(accessGuard, TokenGenerator, externalContributionService);
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
        {
            if (oneTimeToken == "malformed-token")
            {
                throw new ArgumentException("Malformed token.", nameof(oneTimeToken));
            }

            return oneTimeToken == "opaque-once"
                ? new MagicLinkTokenHash("hash-only")
                : new MagicLinkTokenHash("different-hash");
        }
    }
}
