using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.Policies;

/// <summary>Fail-closed default when no per-comment publication authority is configured.</summary>
public sealed class DenyAllTimeEntryCommentPublicationAuthority : ITimeEntryCommentPublicationAuthority
{
    /// <inheritdoc />
    public bool MayPublish(TimesheetsRequestContext context, TimeEntryComment comment) => false;

    /// <inheritdoc />
    public bool MayDisplayExternally(TimesheetsRequestContext context, TimeEntryComment comment) => false;

    /// <inheritdoc />
    public bool MayDisplayUnredactedExternally(TimesheetsRequestContext context, TimeEntryComment comment) => false;
}
