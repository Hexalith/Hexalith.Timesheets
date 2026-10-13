using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.Authorization;

namespace Hexalith.Timesheets.Server.Policies;

/// <summary>Issues a server-owned publication decision for one comment after authorization.</summary>
public interface ITimeEntryCommentPublicationAuthority
{
    /// <summary>Returns true only when this exact comment may enter the evidence read model.</summary>
    bool MayPublish(TimesheetsRequestContext context, TimeEntryComment comment);

    /// <summary>Returns true only when this exact comment may be shown to a scoped external recipient.</summary>
    bool MayDisplayExternally(TimesheetsRequestContext context, TimeEntryComment comment);

    /// <summary>Returns true only when the server explicitly clears this exact comment for unredacted external display.</summary>
    bool MayDisplayUnredactedExternally(TimesheetsRequestContext context, TimeEntryComment comment);
}
