using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.TimeEntries;

namespace Hexalith.Timesheets.Server.MagicLinks;

public sealed record MagicLinkConfirmationUseResult(
    TimesheetsDomainResult? CapabilityResult,
    TimeEntryConfirmationCommandResult? TimeEntryResult)
{
    public bool WasDispatched =>
        CapabilityResult?.IsSuccess == true
        && TimeEntryResult?.WasDispatched == true
        && TimeEntryResult.DomainResult?.IsSuccess == true;
}
