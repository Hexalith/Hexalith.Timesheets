using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Server.TimeEntries;

namespace Hexalith.Timesheets.Server.MagicLinks;

public sealed record MagicLinkEndpointTokenState(
    MagicLinkCapabilityState? CapabilityState,
    TimeEntryState? TimeEntryState,
    ActivityTypeCatalogReadModel ActivityTypeCatalog);
