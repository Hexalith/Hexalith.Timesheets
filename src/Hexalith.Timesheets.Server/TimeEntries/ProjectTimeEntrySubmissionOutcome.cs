namespace Hexalith.Timesheets.Server.TimeEntries;

/// <summary>Describes what the host learned about a Project workflow submission.</summary>
public enum ProjectTimeEntrySubmissionOutcome
{
    /// <summary>The request was refused or the owner made no change.</summary>
    Refused,

    /// <summary>EventStore confirmed exactly one owner event for this command.</summary>
    Committed,

    /// <summary>The host could not verify the command outcome within its observation deadline.</summary>
    Indeterminate
}
