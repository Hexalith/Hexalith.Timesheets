using Hexalith.Timesheets.Contracts.Commands.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.MagicLinks;
using Hexalith.Timesheets.Server.References;

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

    private static CapabilityStateData IssuedState()
    {
        CapabilityStateData state = new();
        state.Apply(new MagicLinkConfirmationCapabilityIssued(
            CapabilityId(),
            Context().Tenant!,
            Contributor(),
            TimeEntryTargetReference.ForProject(Project()),
            ActivityId(),
            TimeEntryId(),
            MagicLinkTargetKind.ProposedTimeEntry,
            MagicLinkAllowedAction.Confirm,
            new MagicLinkTokenHash("hash-only"),
            ExpiresAtUtc(),
            Context().Actor!,
            IssuedAtUtc(),
            new MagicLinkAuditMetadata("timesheets", "issue-1"),
            true));
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

            return new(accessGuard, TokenGenerator);
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
    }
}
