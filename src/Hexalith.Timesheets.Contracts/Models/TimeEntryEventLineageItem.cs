using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Models;

public sealed record TimeEntryEventLineageItem(
    string EventName,
    long Ordinal,
    TimeEntryEvidenceSourceAuthority SourceAuthority);
