namespace Hexalith.Timesheets.Projections;

public sealed record TimesheetsProjectionCheckpoint(
    string TenantId,
    string ProjectionName,
    long SequenceNumber,
    ProjectionFreshness Freshness)
{
    public bool CanServeReads => Freshness == ProjectionFreshness.Fresh;

    /// <summary>Rejects checkpoints that do not certify the requested tenant.</summary>
    /// <param name="tenantId">The tenant whose projection is being folded.</param>
    /// <exception cref="InvalidOperationException">The checkpoint belongs to another tenant.</exception>
    internal void ValidateTenant(string tenantId)
    {
        if (!StringComparer.Ordinal.Equals(TenantId, tenantId))
        {
            throw new InvalidOperationException("Projection checkpoint tenant does not match the requested projection.");
        }
    }
}
