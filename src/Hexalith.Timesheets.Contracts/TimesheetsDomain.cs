namespace Hexalith.Timesheets.Contracts;

/// <summary>Stable domain and issued-publication contract identities shared by module layers.</summary>
public static class TimesheetsDomain
{
    /// <summary>The EventStore domain name.</summary>
    public const string Name = "timesheets";

    /// <summary>The legacy publication version that introduced independent external-display decisions.</summary>
    public const int ExternalCommentPublicationVersion = 2;

    /// <summary>The current publication version, including independent finance decisions.</summary>
    public const int CommentPublicationVersion = 3;
}
