using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Commands.ExternalContributions;

public sealed record ConfirmExternalTimeEntry(
    TimeEntryId TimeEntryId,
    PartyReference Contributor,
    ExternalContributionSource Source);
