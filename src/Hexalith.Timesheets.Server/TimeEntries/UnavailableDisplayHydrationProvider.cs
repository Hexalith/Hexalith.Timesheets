using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.TimeEntries;

public sealed class UnavailableDisplayHydrationProvider :
    IPartyDisplayHydrationProvider,
    IProjectDisplayHydrationProvider,
    IWorkDisplayHydrationProvider,
    IActivityTypeDisplayHydrationProvider
{
    public ValueTask<TimeEntryHydratedDisplayLabel> HydrateContributorAsync(
        TimesheetsRequestContext context,
        PartyReference contributor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(contributor);

        return Unavailable("Contributor display label is unavailable.");
    }

    public ValueTask<TimeEntryHydratedDisplayLabel> HydrateProjectAsync(
        TimesheetsRequestContext context,
        ProjectReference project,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(project);

        return Unavailable("Project display label is unavailable.");
    }

    public ValueTask<TimeEntryHydratedDisplayLabel> HydrateWorkAsync(
        TimesheetsRequestContext context,
        WorkReference work,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(work);

        return Unavailable("Work display label is unavailable.");
    }

    public ValueTask<TimeEntryHydratedDisplayLabel> HydrateActivityTypeAsync(
        TimesheetsRequestContext context,
        ActivityTypeId activityTypeId,
        ActivityTypeScope activityTypeScope,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(activityTypeId);

        return Unavailable("Activity Type display label is unavailable.");
    }

    private static ValueTask<TimeEntryHydratedDisplayLabel> Unavailable(string detail)
        => ValueTask.FromResult(TimeEntryHydratedDisplayLabel.Unavailable(detail));
}
