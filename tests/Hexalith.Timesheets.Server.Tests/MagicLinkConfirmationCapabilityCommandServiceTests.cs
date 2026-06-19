using Hexalith.Timesheets.Contracts.Commands.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.Rejections;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.MagicLinks;
using Hexalith.Timesheets.Server.References;
using Hexalith.Timesheets.Server.TimeEntries;

using NSubstitute;

using Shouldly;

using CapabilityStateData = Hexalith.Timesheets.Server.MagicLinks.MagicLinkCapabilityState;

namespace Hexalith.Timesheets.Server.Tests;

public sealed class MagicLinkConfirmationCapabilityCommandServiceTests
{
    [Fact]
    public async Task Issue_magic_link_stores_hash_and_metadata_only_and_returns_one_time_token()
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
    public async Task Issue_magic_link_fails_closed_on_tenant_authority_before_references_or_token_generation()
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
    public async Task Issue_magic_link_fails_closed_on_invalid_project_before_contributor_policy_or_token_generation()
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
    public async Task Issue_magic_link_fails_closed_on_invalid_contributor_before_policy_or_token_generation()
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
    public async Task Issue_magic_link_fails_closed_on_invalid_work_before_contributor_policy_or_token_generation()
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
    public async Task Issue_magic_link_fails_closed_on_policy_denial_before_token_generation()
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
    public async Task Issue_magic_link_fails_closed_when_activity_catalog_is_stale()
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
    public async Task Issue_magic_link_rejects_inactive_or_ambiguous_activity_type_before_token_generation()
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
    public async Task Issue_magic_link_fails_closed_on_malformed_scope_without_target()
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

    [Fact]
    public async Task Issue_magic_link_rejects_unknown_allowed_action_before_token_generation()
    {
        Fixture fixture = AuthorizedProjectFixture();
        IssueMagicLinkConfirmationCapability command = IssueCommand() with
        {
            AllowedAction = MagicLinkAllowedAction.Unknown
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
    public void Expire_magic_link_rejects_premature_expiry_before_expiry_instant()
    {
        TimesheetsDomainResult result = new Fixture().CreateService().Expire(
            ExpireCommand(),
            IssuedState(),
            Context(),
            IssuedAtUtc());

        result.IsRejection.ShouldBeTrue();
    }

    [Fact]
    public async Task Issue_magic_link_rejects_non_utc_or_expired_at_issue_expiry()
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
    public async Task Issue_magic_link_rejects_duplicate_capability_without_generating_new_token()
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
    public async Task Revoke_magic_link_records_auditable_terminal_state()
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
    public void Expire_magic_link_records_auditable_policy_state()
    {
        MagicLinkCapabilityCommandResult issue = null!;
        TimesheetsDomainResult result = new Fixture().CreateService().Expire(
            ExpireCommand(),
            IssuedState(),
            Context(),
            ExpiresAtUtc());

        issue.ShouldBeNull();
        MagicLinkConfirmationCapabilityExpired expired = result.Events.ShouldHaveSingleItem()
            .ShouldBeOfType<MagicLinkConfirmationCapabilityExpired>();
        expired.ExpiredAtUtc.ShouldBe(ExpiresAtUtc());
    }

    [Fact]
    public async Task Revoke_or_expire_unknown_and_terminal_capabilities_are_rejected()
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
        TimesheetsDomainResult terminalExpire = fixture.CreateService().Expire(
            ExpireCommand(),
            terminal,
            Context(),
            ExpiresAtUtc());

        unknownRevoke.DomainResult.ShouldNotBeNull().IsRejection.ShouldBeTrue();
        terminalExpire.IsRejection.ShouldBeTrue();
    }

    [Fact]
    public async Task Confirm_magic_link_consumes_capability_and_records_contributor_evidence_not_approval()
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
    public async Task Adjust_magic_link_updates_draft_external_values_then_consumes_capability()
    {
        Fixture fixture = AuthorizedProjectFixture();
        AdjustTimeThroughMagicLink command = AdjustCommand();

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
        adjusted.Source.ShouldBe(new ExternalContributionSource("magic-link", "capability-1"));

        MagicLinkConfirmationCapabilityUsed used = result.CapabilityResult.ShouldNotBeNull()
            .Events.ShouldHaveSingleItem()
            .ShouldBeOfType<MagicLinkConfirmationCapabilityUsed>();
        used.OutcomeCategory.ShouldBe("adjusted");
    }

    [Fact]
    public async Task Adjust_magic_link_fails_closed_without_capability_use_when_values_are_invalid()
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
    public async Task Adjust_magic_link_rejects_confirm_only_capability_without_time_entry_adjustment()
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
    public async Task Confirmation_and_adjustment_invalid_link_rejections_have_identical_neutral_messages()
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
    public async Task Invalid_link_categories_all_produce_equivalent_external_service_results_without_dispatch()
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
    public async Task Adjust_invalid_link_categories_all_produce_equivalent_external_service_results_without_dispatch()
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
    public async Task Malformed_magic_link_tokens_are_neutral_for_describe_confirm_and_adjust()
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
    public async Task Unauthorized_magic_link_disclosure_paths_return_no_display_or_dispatch_result()
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
    public async Task Confirm_magic_link_fails_closed_for_invalid_tokens_without_time_entry_confirmation(string token)
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
    public async Task Confirm_magic_link_rejects_reuse_without_time_entry_confirmation()
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
    public async Task Confirm_magic_link_fails_closed_for_unavailable_or_terminal_capabilities_without_time_entry_confirmation()
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
    public async Task Confirm_magic_link_fails_closed_for_expired_tenant_mismatched_wrong_action_or_wrong_scope_state()
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
    public async Task Confirm_magic_link_fails_closed_when_time_entry_state_does_not_match_capability_scope()
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
    public async Task Describe_magic_link_returns_only_safe_confirmation_details_for_valid_token()
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

    [Fact]
    public async Task Describe_magic_link_fails_closed_for_invalid_used_expired_wrong_scope_or_unfresh_states()
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
    public async Task Describe_adjustment_fails_closed_for_invalid_used_expired_wrong_scope_or_unfresh_states()
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
    public async Task Unavailable_magic_link_state_loader_returns_only_fail_closed_state()
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
    public void Invalid_link_outcome_vocabulary_is_internal_and_correlation_safe()
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
        PartyReference? contributor = null)
    {
        TimeEntryState state = new();
        state.Apply(new TimeEntryRecorded(
            timeEntryId ?? TimeEntryId(),
            target ?? TimeEntryTargetReference.ForProject(Project()),
            contributor ?? Contributor(),
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
