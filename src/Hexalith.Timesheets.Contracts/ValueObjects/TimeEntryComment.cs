using Hexalith.Timesheets.Contracts.Policies;

namespace Hexalith.Timesheets.Contracts.ValueObjects;

public sealed record TimeEntryComment
{
    /// <summary>
    /// Upper bound on comment text length. Comments are sensitive unstructured evidence,
    /// so this guardrail keeps payloads bounded until a tenant policy specifies a final limit.
    /// </summary>
    public const int MaxLength = 4096;

    public TimeEntryComment(string text, TimeEntryCommentPolicy policy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(text.Length, MaxLength);
        ArgumentNullException.ThrowIfNull(policy);

        Text = text;
        Policy = policy;
    }

    public string Text { get; }

    public TimeEntryCommentPolicy Policy { get; }
}
