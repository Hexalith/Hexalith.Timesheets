namespace Hexalith.Timesheets.Server.MagicLinks.Commands;

/// <summary>The server-resolved action to commit for a single-use capability.</summary>
public enum MagicLinkUseAction
{
    /// <summary>Confirm recorded time.</summary>
    Confirm = 1,
    /// <summary>Adjust recorded time.</summary>
    Adjust = 2
}
