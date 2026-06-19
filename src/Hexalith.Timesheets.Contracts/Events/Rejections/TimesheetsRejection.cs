namespace Hexalith.Timesheets.Contracts.Events.Rejections;

public sealed record TimesheetsRejection(
    TimesheetsRejectionCode Code,
    string Message,
    IReadOnlyList<TimesheetsFieldError> FieldErrors);
