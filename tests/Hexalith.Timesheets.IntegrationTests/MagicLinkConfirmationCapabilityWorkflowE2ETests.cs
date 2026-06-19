using System.Text.Json;

using Hexalith.Timesheets.Contracts.Commands.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections;
using Hexalith.Timesheets.Projections.MagicLinks;
using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.MagicLinks;

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
        MagicLinkConfirmationCapabilityCommandService service = new(accessGuard, tokenGenerator);

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
    public async Task Magic_link_issue_with_unfresh_activity_catalog_fails_closed_without_token_or_projection()
    {
        RecordingAccessGuard accessGuard = new(TimesheetsAuthorizationDecision.Allowed());
        DeterministicTokenGenerator tokenGenerator = new();
        MagicLinkConfirmationCapabilityCommandService service = new(accessGuard, tokenGenerator);

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

    private static RevokeMagicLinkConfirmationCapability RevokeCommand()
        => new(CapabilityId(), new MagicLinkAuditMetadata("timesheets", "revoke-1"));

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
        => new(new TenantReference("tenant-1"), Operator(), "correlation-1");

    private static DateTimeOffset IssuedAtUtc() => new(2026, 6, 19, 12, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset RevokedAtUtc() => new(2026, 6, 19, 13, 0, 0, TimeSpan.Zero);

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
    }
}
