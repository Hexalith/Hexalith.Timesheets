namespace Hexalith.Timesheets.Contracts.References;

public sealed record TenantReference
{
    public TenantReference(string tenantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        TenantId = tenantId;
    }

    public string TenantId { get; }
}
