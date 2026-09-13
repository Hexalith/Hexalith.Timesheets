using Hexalith.Timesheets.Server.MagicLinks;

namespace Hexalith.Timesheets.Projections.MagicLinks;

internal sealed record IndexRebuildCandidateEntry(
    string TokenHash,
    MagicLinkTokenHashCapabilityIndexEntry Candidate);
