namespace Hexalith.Timesheets.Contracts.Ui;

public sealed record TimesheetsMetadataDescriptor(
    string Name,
    string Title,
    string Capability,
    TimesheetsSurfaceKind SurfaceKind,
    TimesheetsCompositionPattern Pattern,
    IReadOnlyList<TimesheetsMetadataFieldDescriptor> Fields,
    IReadOnlyList<TimesheetsMetadataActionDescriptor> Actions,
    IReadOnlyList<TimesheetsMetadataStateBadgeDescriptor> StateBadges);
