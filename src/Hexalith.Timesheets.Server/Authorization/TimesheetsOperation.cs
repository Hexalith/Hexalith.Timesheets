namespace Hexalith.Timesheets.Server.Authorization;

public enum TimesheetsOperation
{
    Unknown = 0,
    Command = 1,
    Query = 2,
    ProjectionRead = 3,
    Export = 4,
    Confirmation = 5,
    UiActionVisibility = 6
}
