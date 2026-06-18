namespace Hexalith.Timesheets.Contracts;

public static class TimesheetsMetadataCatalog
{
    public static IReadOnlyList<TimesheetsMetadataDescriptor> Descriptors { get; } =
    [
        new(
            "timesheets.frontcomposer.entry-points",
            "FrontComposer-compatible metadata extension point for future generated command and projection surfaces.",
            "metadata"),
        new(
            "timesheets.eventstore.domain-service",
            "EventStore domain-service registration point for future Timesheets aggregates, commands, events, and queries.",
            "runtime")
    ];
}
