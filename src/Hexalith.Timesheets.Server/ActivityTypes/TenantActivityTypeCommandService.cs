using Hexalith.Timesheets.Contracts.Commands.ActivityTypes;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.ActivityTypes;

public sealed class TenantActivityTypeCommandService
{
    private readonly ITimesheetsAccessGuard _accessGuard;

    public TenantActivityTypeCommandService(ITimesheetsAccessGuard accessGuard)
    {
        ArgumentNullException.ThrowIfNull(accessGuard);
        _accessGuard = accessGuard;
    }

    public ValueTask<ActivityTypeCommandResult> CreateAsync(
        TimesheetsRequestContext context,
        CreateTenantActivityType command,
        ActivityTypeCatalogState? state,
        CancellationToken cancellationToken)
        => ExecuteTenantWriteAsync(
            context,
            () => TenantActivityTypeAggregate.Handle(command, state),
            cancellationToken);

    public ValueTask<ActivityTypeCommandResult> RenameAsync(
        TimesheetsRequestContext context,
        RenameActivityType command,
        ActivityTypeCatalogState? state,
        CancellationToken cancellationToken)
        => ExecuteTenantWriteAsync(
            context,
            () => TenantActivityTypeAggregate.Handle(command, state),
            cancellationToken);

    public ValueTask<ActivityTypeCommandResult> UpdateMetadataAsync(
        TimesheetsRequestContext context,
        UpdateActivityTypeMetadata command,
        ActivityTypeCatalogState? state,
        CancellationToken cancellationToken)
        => ExecuteTenantWriteAsync(
            context,
            () => TenantActivityTypeAggregate.Handle(command, state),
            cancellationToken);

    public ValueTask<ActivityTypeCommandResult> DeactivateAsync(
        TimesheetsRequestContext context,
        DeactivateActivityType command,
        ActivityTypeCatalogState? state,
        CancellationToken cancellationToken)
        => ExecuteTenantWriteAsync(
            context,
            () => TenantActivityTypeAggregate.Handle(command, state),
            cancellationToken);

    public ValueTask<ActivityTypeCommandResult> ReactivateAsync(
        TimesheetsRequestContext context,
        ReactivateActivityType command,
        ActivityTypeCatalogState? state,
        CancellationToken cancellationToken)
        => ExecuteTenantWriteAsync(
            context,
            () => TenantActivityTypeAggregate.Handle(command, state),
            cancellationToken);

    public async ValueTask<TimesheetsAuthorizationDecision> AuthorizeCatalogReadAsync(
        TimesheetsRequestContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        return await _accessGuard.AuthorizeAsync(
            new TimesheetsAuthorizationRequest(context, TimesheetsOperation.ProjectionRead),
            cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<ActivityTypeCommandResult> ExecuteTenantWriteAsync(
        TimesheetsRequestContext context,
        Func<TimesheetsDomainResult> trustedDecision,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(trustedDecision);

        TimesheetsAuthorizationDecision authorization = await _accessGuard.AuthorizeAsync(
            new TimesheetsAuthorizationRequest(context, TimesheetsOperation.Command),
            cancellationToken).ConfigureAwait(false);

        if (!authorization.IsAuthorized)
        {
            return new(authorization, null);
        }

        return new(authorization, trustedDecision());
    }
}
