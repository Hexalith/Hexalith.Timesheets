using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.ApprovalAuthority;

public sealed record ApprovalAuthorityResolutionResult(
    bool IsAllowed,
    TimesheetsDenialCategory DenialCategory,
    string Reason,
    ApprovalAuthoritySourceAttribution SourceAttribution)
{
    public static ApprovalAuthorityResolutionResult Allowed(ApprovalAuthoritySourceAttribution sourceAttribution)
    {
        ArgumentNullException.ThrowIfNull(sourceAttribution);

        return new(true, TimesheetsDenialCategory.None, "authorized", sourceAttribution);
    }

    public static ApprovalAuthorityResolutionResult Denied(
        TimesheetsDenialCategory denialCategory,
        string reason,
        ApprovalAuthoritySourceAttribution sourceAttribution)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        ArgumentNullException.ThrowIfNull(sourceAttribution);

        if (denialCategory == TimesheetsDenialCategory.None)
        {
            throw new ArgumentOutOfRangeException(nameof(denialCategory), denialCategory, "Denied authority results require a denial category.");
        }

        return new(false, denialCategory, reason, sourceAttribution);
    }
}
