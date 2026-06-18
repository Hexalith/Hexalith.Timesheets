namespace Hexalith.Timesheets.Server.Authorization;

public enum TimesheetsOperation
{
    Unknown = 0,
    Command = 1,
    Query = 2,
    Projection = 3,
    Export = 4
}
