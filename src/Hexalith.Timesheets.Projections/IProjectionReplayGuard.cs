namespace Hexalith.Timesheets.Projections;

public interface IProjectionReplayGuard
{
    bool ShouldApply(string messageId);
}
