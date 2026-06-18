using Hexalith.Timesheets.Contracts.References;

namespace Hexalith.Timesheets.Testing;

public static class TimesheetsTestIds
{
    public static TenantReference Tenant { get; } = new("tenant-test");

    public static PartyReference Actor { get; } = new("party-actor");

    public static ProjectReference Project { get; } = new("project-test");

    public static WorkReference Work { get; } = new("work-test");
}
