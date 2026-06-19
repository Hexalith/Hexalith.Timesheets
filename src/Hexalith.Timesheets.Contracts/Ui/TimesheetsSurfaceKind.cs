using System.Text.Json.Serialization;

namespace Hexalith.Timesheets.Contracts.Ui;

[JsonConverter(typeof(JsonStringEnumConverter<TimesheetsSurfaceKind>))]
public enum TimesheetsSurfaceKind
{
    Unknown = 0,
    Command = 1,
    Projection = 2
}
