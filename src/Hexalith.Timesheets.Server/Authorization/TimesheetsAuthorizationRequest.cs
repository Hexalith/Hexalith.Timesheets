using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Server.Authorization;

public sealed record TimesheetsAuthorizationRequest(
    TimesheetsRequestContext Context,
    TimesheetsOperation Operation)
{
    public ProjectReference? Project { get; init; }

    public WorkReference? Work { get; init; }

    public PartyReference? Contributor { get; init; }

    public TimesheetsUiAction? UiAction { get; init; }

    public ApprovalAuthorityAction ApprovalAction { get; init; }
}
