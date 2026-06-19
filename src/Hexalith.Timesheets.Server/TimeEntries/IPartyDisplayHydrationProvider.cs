using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.TimeEntries;

public interface IPartyDisplayHydrationProvider
{
    ValueTask<TimeEntryHydratedDisplayLabel> HydrateContributorAsync(
        TimesheetsRequestContext context,
        PartyReference contributor,
        CancellationToken cancellationToken);
}
