namespace Hexalith.Timesheets.Contracts.Models.MagicLinks;

public sealed record MagicLinkInvalidLinkDenial(string Title, string Detail, string RecoveryPath)
{
    public const string DefaultTitle = "This link is expired or unavailable.";

    public const string DefaultDetail = "Request a new confirmation link from the sender.";

    public static readonly MagicLinkInvalidLinkDenial Default = new(
        DefaultTitle,
        DefaultDetail,
        DefaultDetail);
}
