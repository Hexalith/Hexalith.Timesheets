namespace Hexalith.Timesheets.Contracts.Ui;

public sealed record TimesheetsMetadataFieldDescriptor(
    string Name,
    string Label,
    string ContractType,
    bool IsRequired);
