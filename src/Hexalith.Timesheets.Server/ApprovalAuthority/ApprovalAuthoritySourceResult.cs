using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.ApprovalAuthority;

public sealed record ApprovalAuthoritySourceResult(
    ApprovalAuthoritySource Source,
    bool IsAllowed,
    ApprovalAuthorityDecisionState DecisionState,
    TimesheetsDenialCategory DenialCategory,
    string Reason,
    ProjectionFreshnessMetadata Freshness)
{
    public static ApprovalAuthoritySourceResult Allowed(
        ApprovalAuthoritySource source,
        ProjectionFreshnessMetadata freshness)
    {
        ArgumentNullException.ThrowIfNull(freshness);

        return new(
            source,
            true,
            ApprovalAuthorityDecisionState.Allowed,
            TimesheetsDenialCategory.None,
            "authorized",
            freshness);
    }

    public static ApprovalAuthoritySourceResult Denied(
        ApprovalAuthoritySource source,
        ApprovalAuthorityDecisionState decisionState,
        TimesheetsDenialCategory denialCategory,
        string reason,
        ProjectionFreshnessMetadata freshness)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentNullException.ThrowIfNull(freshness);

        if (denialCategory == TimesheetsDenialCategory.None)
        {
            throw new ArgumentOutOfRangeException(nameof(denialCategory), denialCategory, "Denied source results require a denial category.");
        }

        return new(source, false, decisionState, denialCategory, reason, freshness);
    }

    public static ApprovalAuthoritySourceResult Unavailable(ApprovalAuthoritySource source)
    {
        return Denied(
            source,
            ApprovalAuthorityDecisionState.Unavailable,
            TimesheetsDenialCategory.UnavailableSiblingAuthority,
            "Authority cannot be resolved.",
            ProjectionFreshnessMetadata.Unavailable());
    }
}
