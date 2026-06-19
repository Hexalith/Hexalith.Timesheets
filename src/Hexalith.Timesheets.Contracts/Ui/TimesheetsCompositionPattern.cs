using System.Text.Json.Serialization;

namespace Hexalith.Timesheets.Contracts.Ui;

[JsonConverter(typeof(JsonStringEnumConverter<TimesheetsCompositionPattern>))]
public enum TimesheetsCompositionPattern
{
    Unknown = 0,
    FrontComposerGeneratedForm = 1,
    FrontComposerProjectionView = 2
}
