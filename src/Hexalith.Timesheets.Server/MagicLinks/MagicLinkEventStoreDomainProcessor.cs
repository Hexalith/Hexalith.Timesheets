using System.Text.Json;

using Hexalith.EventStore.Client.Handlers;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Events;
using Hexalith.EventStore.Contracts.Results;
using Hexalith.Timesheets.Contracts.Commands.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.MagicLinks.Commands;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.Runtime;
using Hexalith.Timesheets.Server.TimeEntries;

namespace Hexalith.Timesheets.Server.MagicLinks;

/// <summary>Processes magic-link use against the Time Entry actor's current state.</summary>
public sealed class MagicLinkEventStoreDomainProcessor(
    EventStoreMagicLinkConfirmationCapabilityStateLoader stateLoader,
    MagicLinkConfirmationCapabilityCommandService commandService,
    TimeProvider clock) : IDomainProcessor, IAsyncDomainProcessor
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    /// <inheritdoc/>
    public Task<DomainResult> ProcessAsync(CommandEnvelope command, object? currentState)
        => ProcessAsync(command, currentState, CancellationToken.None);

    /// <inheritdoc/>
    public async Task<DomainResult> ProcessAsync(
        CommandEnvelope command,
        object? currentState,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        cancellationToken.ThrowIfCancellationRequested();
        if (!StringComparer.Ordinal.Equals(command.Domain, TimesheetsEventStoreIntegration.DomainName))
        {
            return Reject();
        }

        if (!HasVerifiedWorkloadOrigin(command))
        {
            return Reject();
        }

        if (StringComparer.Ordinal.Equals(command.CommandType, typeof(CommitMagicLinkIssue).FullName))
        {
            return await ProcessIssueAsync(command, currentState, cancellationToken).ConfigureAwait(false);
        }

        if (StringComparer.Ordinal.Equals(command.CommandType, typeof(CommitMagicLinkTransition).FullName))
        {
            return await ProcessTransitionAsync(command, currentState, cancellationToken).ConfigureAwait(false);
        }

        if (!StringComparer.Ordinal.Equals(command.CommandType, typeof(CommitMagicLinkUse).FullName))
        {
            return Reject();
        }

        CommitMagicLinkUse? intent;
        TimeEntryState? timeEntry;
        try
        {
            intent = JsonSerializer.Deserialize<CommitMagicLinkUse>(command.Payload, _jsonOptions);
            timeEntry = DomainStateReplay.Rehydrate<TimeEntryState>(currentState);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException)
        {
            return Reject();
        }

        if (intent?.CapabilityId is null
            || intent.Tenant is null
            || intent.TimeEntryId is null
            || intent.TokenHash is null
            || timeEntry?.IsRecorded != true
            || timeEntry.HasAmbiguousMagicLinkTerminalHistory
            || intent.UsedAtUtc.Offset != TimeSpan.Zero
            || !StringComparer.Ordinal.Equals(command.TenantId, intent.Tenant.TenantId)
            || !StringComparer.Ordinal.Equals(command.AggregateId, intent.TimeEntryId.Value)
            || timeEntry.TimeEntryId != intent.TimeEntryId
            || timeEntry.HasTerminalMagicLinkCapability(intent.CapabilityId)
            || intent.Action is not (MagicLinkUseAction.Confirm or MagicLinkUseAction.Adjust)
            || (intent.Action == MagicLinkUseAction.Confirm && intent.Adjustment is not null)
            || (intent.Action == MagicLinkUseAction.Adjust && intent.Adjustment is null))
        {
            return Reject();
        }

        MagicLinkCapabilityState? capability = await stateLoader.LoadCapabilityStreamForTenantAsync(
            intent.Tenant, intent.CapabilityId, cancellationToken).ConfigureAwait(false);
        if (capability?.Exists != true
            || capability.IsTerminal
            || capability.Tenant != intent.Tenant
            || capability.TimeEntryId != intent.TimeEntryId
            || capability.TokenHash != intent.TokenHash)
        {
            return Reject();
        }

        ActivityTypeCatalogReadModel catalog = await stateLoader.LoadCatalogForTenantAsync(
            intent.Tenant, cancellationToken).ConfigureAwait(false);
        if (catalog.ProjectionFreshness.State != ProjectionFreshnessState.Fresh)
        {
            return Reject();
        }

        // The HTTP instant is audit input, not an expiry decision for a queued command.
        // Use the actor's processing clock immediately before making the use decision.
        DateTimeOffset processingAtUtc = clock.GetUtcNow();
        if (processingAtUtc >= capability.ExpiresAtUtc)
        {
            return Reject();
        }

        TimesheetsRequestContext context = MagicLinkExternalRequestContext.FromResolvedCapability(
            capability, command.CorrelationId);
        MagicLinkConfirmationUseResult result = intent.Action == MagicLinkUseAction.Confirm
            ? await commandService.ConfirmResolvedAsync(
                context,
                intent.TokenHash,
                new ConfirmTimeThroughMagicLink(),
                capability,
                timeEntry,
                intent.UsedAtUtc,
                cancellationToken).ConfigureAwait(false)
            : await commandService.AdjustResolvedAsync(
                context,
                intent.TokenHash,
                intent.Adjustment!,
                capability,
                timeEntry,
                catalog,
                intent.UsedAtUtc,
                cancellationToken).ConfigureAwait(false);

        if (!result.WasDispatched
            || result.CapabilityResult?.Events is not [MagicLinkConfirmationCapabilityUsed used]
            || (intent.Action == MagicLinkUseAction.Confirm
                && result.TimeEntryResult?.DomainResult?.Events is not [TimeEntryContributorConfirmed])
            || (intent.Action == MagicLinkUseAction.Adjust
                && result.AdjustmentResult?.DomainResult?.Events is not [TimeEntryAdjustedThroughMagicLink]))
        {
            return Reject();
        }

        // Authorization and domain-service checks can await beyond the expiry instant.
        // The original HTTP instant remains the audit value; this is the commit decision.
        if (clock.GetUtcNow() >= capability.ExpiresAtUtc)
        {
            return Reject();
        }

        IEventPayload effect = intent.Action == MagicLinkUseAction.Confirm
            ? new StoredTimeEntryConfirmed((TimeEntryContributorConfirmed)result.TimeEntryResult!.DomainResult!.Events[0])
            : new StoredTimeEntryAdjusted((TimeEntryAdjustedThroughMagicLink)result.AdjustmentResult!.DomainResult!.Events[0]);
        return DomainResult.Success([new StoredMagicLinkUsed(used), effect]);
    }

    private async Task<DomainResult> ProcessIssueAsync(
        CommandEnvelope envelope,
        object? currentState,
        CancellationToken cancellationToken)
    {
        CommitMagicLinkIssue? intent;
        MagicLinkCapabilityState? capability;
        try
        {
            intent = JsonSerializer.Deserialize<CommitMagicLinkIssue>(envelope.Payload, _jsonOptions);
            capability = DomainStateReplay.Rehydrate<MagicLinkCapabilityState>(currentState);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException)
        {
            return Reject();
        }

        if (intent?.Command?.CapabilityId is null
            || intent.Command.Scope is null
            || intent.Command.Scope.Contributor is null
            || intent.Command.Scope.Target is null
            || string.IsNullOrWhiteSpace(intent.Command.Scope.Target.TargetId)
            || intent.Command.Scope.ActivityTypeId is null
            || intent.Command.Scope.TimeEntryId is null
            || intent.Command.Source is null
            || string.IsNullOrWhiteSpace(intent.Command.Source.SourceSystem)
            || string.IsNullOrWhiteSpace(intent.Command.Source.SourceReference)
            || intent.Tenant is null
            || intent.Issuer is null
            || intent.TokenHash is null
            || !HasVerifiedActor(envelope, intent.Issuer.PartyId)
            || intent.IssuedAtUtc.Offset != TimeSpan.Zero
            || clock.GetUtcNow() >= intent.Command.ExpiresAtUtc
            || envelope.TenantId != intent.Tenant.TenantId
            || envelope.AggregateId != intent.Command.CapabilityId.Value
            || StringComparer.Ordinal.Equals(intent.Command.Scope.TimeEntryId.Value, intent.Command.CapabilityId.Value)
            || (capability is not null && (capability.Exists || capability.IsTerminal)))
        {
            return Reject();
        }

        ActivityTypeCatalogReadModel catalog = await stateLoader.LoadCatalogForTenantAsync(
            intent.Tenant, cancellationToken).ConfigureAwait(false);
        if (clock.GetUtcNow() >= intent.Command.ExpiresAtUtc)
        {
            return Reject();
        }

        TimesheetsRequestContext context = TimesheetsServerRequestContext.FromTrustedSources(
            intent.Tenant.TenantId, intent.Issuer.PartyId, envelope.CorrelationId);
        MagicLinkCapabilityCommandResult result = await commandService.IssueResolvedAsync(
            context, intent.Command, capability, catalog, intent.TokenHash, intent.IssuedAtUtc, cancellationToken)
            .ConfigureAwait(false);
        return result.Authorization.IsAuthorized
            && clock.GetUtcNow() < intent.Command.ExpiresAtUtc
            && result.DomainResult?.Events is [MagicLinkConfirmationCapabilityIssued issued]
                ? DomainResult.Success([new StoredMagicLinkIssued(issued)])
                : Reject();
    }

    private async Task<DomainResult> ProcessTransitionAsync(
        CommandEnvelope envelope,
        object? currentState,
        CancellationToken cancellationToken)
    {
        CommitMagicLinkTransition? intent;
        TimeEntryState? timeEntry;
        try
        {
            intent = JsonSerializer.Deserialize<CommitMagicLinkTransition>(envelope.Payload, _jsonOptions);
            timeEntry = DomainStateReplay.Rehydrate<TimeEntryState>(currentState);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or ArgumentException)
        {
            return Reject();
        }

        if (intent?.CapabilityId is null
            || intent.Tenant is null
            || intent.TimeEntryId is null
            || intent.Source is null
            || (intent.Action == MagicLinkTransitionAction.Revoke
                && (intent.Actor is null || !HasVerifiedActor(envelope, intent.Actor.PartyId)))
            || (intent.Action == MagicLinkTransitionAction.Expire
                && intent.Actor is not null && !HasVerifiedActor(envelope, intent.Actor.PartyId))
            || (timeEntry is not null
                && (timeEntry.HasAmbiguousMagicLinkTerminalHistory
                    || (timeEntry.IsRecorded && timeEntry.TimeEntryId != intent.TimeEntryId)
                    || (!timeEntry.IsRecorded && timeEntry.TimeEntryId is not null)
                    || timeEntry.HasTerminalMagicLinkCapability(intent.CapabilityId)))
            || intent.AtUtc.Offset != TimeSpan.Zero
            || envelope.TenantId != intent.Tenant.TenantId
            || envelope.AggregateId != intent.TimeEntryId.Value
            || intent.Action is not (MagicLinkTransitionAction.Revoke or MagicLinkTransitionAction.Expire))
        {
            return Reject();
        }

        MagicLinkCapabilityState? capability = await stateLoader.LoadCapabilityStreamForTenantAsync(
            intent.Tenant, intent.CapabilityId, cancellationToken).ConfigureAwait(false);
        if (capability?.Exists != true
            || capability.IsTerminal
            || capability.Tenant != intent.Tenant
            || capability.TimeEntryId != intent.TimeEntryId
            || (timeEntry?.IsRecorded != true && capability.TargetKind != MagicLinkTargetKind.ProposedTimeEntry))
        {
            return Reject();
        }

        TimesheetsRequestContext context = TimesheetsServerRequestContext.FromTrustedSources(
            intent.Tenant.TenantId, intent.Actor?.PartyId ?? envelope.UserId, envelope.CorrelationId);
        MagicLinkCapabilityCommandResult result = intent.Action == MagicLinkTransitionAction.Revoke
            ? await commandService.RevokeAsync(
                context,
                new RevokeMagicLinkConfirmationCapability(intent.CapabilityId, intent.Source),
                capability,
                intent.AtUtc,
                cancellationToken).ConfigureAwait(false)
            : await commandService.ExpireResolvedAsync(
                context,
                new ExpireMagicLinkConfirmationCapability(intent.CapabilityId, intent.Source),
                capability,
                intent.AtUtc,
                cancellationToken).ConfigureAwait(false);

        return result.Authorization.IsAuthorized
            ? result.DomainResult?.Events switch
            {
                [MagicLinkConfirmationCapabilityRevoked revoked] =>
                    DomainResult.Success([new StoredMagicLinkRevoked(revoked with { TimeEntryId = intent.TimeEntryId })]),
                [MagicLinkConfirmationCapabilityExpired expired] =>
                    DomainResult.Success([new StoredMagicLinkExpired(expired with { TimeEntryId = intent.TimeEntryId })]),
                _ => Reject()
            }
            : Reject();
    }

    private static DomainResult Reject() => DomainResult.Rejection([new MagicLinkCommitRejected()]);

    private static bool HasVerifiedWorkloadOrigin(CommandEnvelope command)
        => command.Extensions is not null
            && command.Extensions.TryGetValue(EventStoreGatewayVerifiedOrigin.ExtensionKey, out string? origin)
            && string.Equals(origin, "timesheets", StringComparison.Ordinal);

    private static bool HasVerifiedActor(CommandEnvelope command, string actor)
        => !string.IsNullOrWhiteSpace(actor)
            && string.Equals(command.UserId, actor, StringComparison.Ordinal)
            && command.Extensions is not null
            && command.Extensions.TryGetValue(EventStoreGatewayVerifiedOrigin.ActorExtensionKey, out string? verifiedActor)
            && string.Equals(verifiedActor, actor, StringComparison.Ordinal);
}
