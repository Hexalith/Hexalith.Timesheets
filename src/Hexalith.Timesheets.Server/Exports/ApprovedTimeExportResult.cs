using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.Exports;

public sealed record ApprovedTimeExportResult(
    TimesheetsAuthorizationDecision Authorization,
    ApprovedTimeExportReadModel? Export,
    TimesheetsDomainResult? AuditResult = null)
{
    public bool WasGenerated => Authorization.IsAuthorized
        && Export?.HasOutput == true;

    public static ApprovedTimeExportResult Generated(ApprovedTimeExportReadModel export, TimesheetsDomainResult auditResult)
    {
        ArgumentNullException.ThrowIfNull(export);
        ArgumentNullException.ThrowIfNull(auditResult);

        return new(TimesheetsAuthorizationDecision.Allowed(), export, auditResult);
    }

    public static ApprovedTimeExportResult Blocked(ApprovedTimeExportReadModel export)
    {
        ArgumentNullException.ThrowIfNull(export);

        return new(TimesheetsAuthorizationDecision.Allowed(), export);
    }

    public static ApprovedTimeExportResult NotFoundOrDenied(TimesheetsAuthorizationDecision authorization)
        => new(authorization, null);
}
