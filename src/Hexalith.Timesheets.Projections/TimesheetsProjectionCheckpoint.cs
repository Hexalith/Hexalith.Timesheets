namespace Hexalith.Timesheets.Projections;

public sealed record TimesheetsProjectionCheckpoint(
    string TenantId,
    string ProjectionName,
    long SequenceNumber,
    ProjectionFreshness Freshness)
{
    public bool CanServeReads => Freshness == ProjectionFreshness.Fresh;
}
