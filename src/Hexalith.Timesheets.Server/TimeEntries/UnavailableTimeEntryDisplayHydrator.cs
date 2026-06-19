using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.TimeEntries;

public sealed class UnavailableTimeEntryDisplayHydrator : ITimeEntryDisplayHydrator
{
    private readonly IActivityTypeDisplayHydrationProvider _activityTypeProvider;
    private readonly IPartyDisplayHydrationProvider _partyProvider;
    private readonly IProjectDisplayHydrationProvider _projectProvider;
    private readonly IWorkDisplayHydrationProvider _workProvider;

    public UnavailableTimeEntryDisplayHydrator(
        IPartyDisplayHydrationProvider partyProvider,
        IProjectDisplayHydrationProvider projectProvider,
        IWorkDisplayHydrationProvider workProvider,
        IActivityTypeDisplayHydrationProvider activityTypeProvider)
    {
        ArgumentNullException.ThrowIfNull(partyProvider);
        ArgumentNullException.ThrowIfNull(projectProvider);
        ArgumentNullException.ThrowIfNull(workProvider);
        ArgumentNullException.ThrowIfNull(activityTypeProvider);

        _partyProvider = partyProvider;
        _projectProvider = projectProvider;
        _workProvider = workProvider;
        _activityTypeProvider = activityTypeProvider;
    }

    public ValueTask<TimeEntryDisplayHydration> HydrateAsync(
        TimesheetsRequestContext context,
        TimeEntryEvidenceReadModel evidence,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(evidence);

        return HydrateCoreAsync(context, evidence, cancellationToken);
    }

    private async ValueTask<TimeEntryDisplayHydration> HydrateCoreAsync(
        TimesheetsRequestContext context,
        TimeEntryEvidenceReadModel evidence,
        CancellationToken cancellationToken)
    {
        TimeEntryHydratedDisplayLabel contributor = await _partyProvider
            .HydrateContributorAsync(context, evidence.Contributor, cancellationToken)
            .ConfigureAwait(false);
        TimeEntryHydratedDisplayLabel target = await HydrateTargetAsync(context, evidence, cancellationToken)
            .ConfigureAwait(false);
        TimeEntryHydratedDisplayLabel activityType = await _activityTypeProvider
            .HydrateActivityTypeAsync(context, evidence.ActivityTypeId, evidence.ActivityTypeScope, cancellationToken)
            .ConfigureAwait(false);

        return new(contributor, target, activityType);
    }

    private ValueTask<TimeEntryHydratedDisplayLabel> HydrateTargetAsync(
        TimesheetsRequestContext context,
        TimeEntryEvidenceReadModel evidence,
        CancellationToken cancellationToken)
        => evidence.Target.TargetKind switch
        {
            TimeEntryTargetKind.Project => _projectProvider.HydrateProjectAsync(
                context,
                new ProjectReference(evidence.Target.TargetId),
                cancellationToken),
            TimeEntryTargetKind.Work => _workProvider.HydrateWorkAsync(
                context,
                new WorkReference(evidence.Target.TargetId),
                cancellationToken),
            _ => ValueTask.FromResult(TimeEntryHydratedDisplayLabel.Unavailable("Target display label is unavailable."))
        };
}
