namespace Hexalith.Timesheets.Contracts;

/// <summary>Stable domain and issued-publication contract identities shared by module layers.</summary>
public static class TimesheetsDomain
{
    /// <summary>The EventStore domain name.</summary>
    public const string Name = "timesheets";

    /// <summary>The current server-issued comment publication decision version.</summary>
    public const int CommentPublicationVersion = 2;
}
