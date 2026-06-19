using Hexalith.Timesheets.Contracts.Commands.ActivityTypes;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.ActivityTypes;

public sealed class ProjectActivityTypeCommandService
{
    private readonly ITimesheetsAccessGuard _accessGuard;

    public ProjectActivityTypeCommandService(ITimesheetsAccessGuard accessGuard)
    {
        ArgumentNullException.ThrowIfNull(accessGuard);
        _accessGuard = accessGuard;
    }

    public ValueTask<ActivityTypeCommandResult> CreateAsync(
        TimesheetsRequestContext context,
        CreateProjectActivityType command,
        ActivityTypeCatalogState? state,
        CancellationToken cancellationToken)
        => ExecuteProjectWriteAsync(
            context,
            command.Project,
            () => ProjectActivityTypeAggregate.Handle(command, state),
            cancellationToken);

    public ValueTask<ActivityTypeCommandResult> RenameAsync(
        TimesheetsRequestContext context,
        RenameProjectActivityType command,
        ActivityTypeCatalogState? state,
        CancellationToken cancellationToken)
        => ExecuteProjectWriteAsync(
            context,
            command.Project,
            () => ProjectActivityTypeAggregate.Handle(command, state),
            cancellationToken);

    public ValueTask<ActivityTypeCommandResult> UpdateMetadataAsync(
        TimesheetsRequestContext context,
        UpdateProjectActivityTypeMetadata command,
        ActivityTypeCatalogState? state,
        CancellationToken cancellationToken)
        => ExecuteProjectWriteAsync(
            context,
            command.Project,
            () => ProjectActivityTypeAggregate.Handle(command, state),
            cancellationToken);

    public ValueTask<ActivityTypeCommandResult> DeactivateAsync(
        TimesheetsRequestContext context,
        DeactivateProjectActivityType command,
        ActivityTypeCatalogState? state,
        CancellationToken cancellationToken)
        => ExecuteProjectWriteAsync(
            context,
            command.Project,
            () => ProjectActivityTypeAggregate.Handle(command, state),
            cancellationToken);

    public ValueTask<ActivityTypeCommandResult> ReactivateAsync(
        TimesheetsRequestContext context,
        ReactivateProjectActivityType command,
        ActivityTypeCatalogState? state,
        CancellationToken cancellationToken)
        => ExecuteProjectWriteAsync(
            context,
            command.Project,
            () => ProjectActivityTypeAggregate.Handle(command, state),
            cancellationToken);

    public ValueTask<ActivityTypeCommandResult> ConfigureRestrictionAsync(
        TimesheetsRequestContext context,
        ConfigureProjectActivityTypeCatalogRestriction command,
        CancellationToken cancellationToken)
        => ExecuteProjectWriteAsync(
            context,
            command.Project,
            () => ProjectActivityTypeAggregate.Handle(command),
            cancellationToken);

    private async ValueTask<ActivityTypeCommandResult> ExecuteProjectWriteAsync(
        TimesheetsRequestContext context,
        ProjectReference project,
        Func<TimesheetsDomainResult> trustedDecision,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(trustedDecision);

        TimesheetsAuthorizationDecision authorization = await _accessGuard.AuthorizeAsync(
            new TimesheetsAuthorizationRequest(context, TimesheetsOperation.Command)
            {
                Project = project
            },
            cancellationToken).ConfigureAwait(false);

        if (!authorization.IsAuthorized)
        {
            return new(authorization, null);
        }

        return new(authorization, trustedDecision());
    }
}
