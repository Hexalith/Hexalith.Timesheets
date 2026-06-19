using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Models;

public sealed record TimeEntryContributorConfirmationEvidence(
    TimeEntryId TimeEntryId,
    PartyReference Contributor,
    DateTimeOffset ConfirmedAtUtc,
    ExternalContributionSource Source);
