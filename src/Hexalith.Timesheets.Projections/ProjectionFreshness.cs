namespace Hexalith.Timesheets.Projections;

public enum ProjectionFreshness
{
    Unknown = 0,
    Rebuilding = 1,
    Fresh = 2,
    Stale = 3,
    Unavailable = 4,
    Degraded = 5
}
