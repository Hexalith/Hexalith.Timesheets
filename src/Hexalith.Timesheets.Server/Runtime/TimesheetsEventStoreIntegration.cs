namespace Hexalith.Timesheets.Server.Runtime;

public static class TimesheetsEventStoreIntegration
{
    public const string DomainName = "timesheets";

    public static Type RegistrationAssemblyMarker { get; } = typeof(TimesheetsServerMarker);
}
