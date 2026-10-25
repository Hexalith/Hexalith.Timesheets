namespace Hexalith.Timesheets.Server.Runtime;

public static class TimesheetsEventStoreIntegration
{
    public const string DomainName = Hexalith.Timesheets.Contracts.TimesheetsDomain.Name;

    public static Type RegistrationAssemblyMarker { get; } = typeof(TimesheetsServerMarker);
}
