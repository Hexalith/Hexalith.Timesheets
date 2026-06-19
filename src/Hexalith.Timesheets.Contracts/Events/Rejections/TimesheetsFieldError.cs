namespace Hexalith.Timesheets.Contracts.Events.Rejections;

public sealed record TimesheetsFieldError(
    string Field,
    string Code,
    string Message);
