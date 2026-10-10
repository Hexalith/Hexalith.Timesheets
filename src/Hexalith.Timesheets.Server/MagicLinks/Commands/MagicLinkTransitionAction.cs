namespace Hexalith.Timesheets.Server.MagicLinks.Commands;

/// <summary>The terminal action requested by an authorized administrator.</summary>
public enum MagicLinkTransitionAction
{
    /// <summary>Revoke an issued capability.</summary>
    Revoke = 1,
    /// <summary>Expire an issued capability.</summary>
    Expire = 2
}
