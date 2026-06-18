namespace Hexalith.Timesheets.Server.Authorization;

public sealed record TimesheetsTenantAccessResult(TimesheetsTenantAccessState State, string Reason)
{
    public bool IsAuthorized => State == TimesheetsTenantAccessState.Authorized;

    public static TimesheetsTenantAccessResult Authorized()
    {
        return new(TimesheetsTenantAccessState.Authorized, "authorized");
    }

    public static TimesheetsTenantAccessResult Denied(
        TimesheetsTenantAccessState state,
        string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (state == TimesheetsTenantAccessState.Authorized)
        {
            throw new ArgumentOutOfRangeException(nameof(state), state, "Denied tenant access requires a denial state.");
        }

        return new(state, reason);
    }
}
