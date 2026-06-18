using Hexalith.Timesheets.Contracts.References;

namespace Hexalith.Timesheets.Server.Authorization;

public sealed record TimesheetsAuthorizationRequest(
    TimesheetsRequestContext Context,
    TimesheetsOperation Operation)
{
    public ProjectReference? Project { get; init; }

    public WorkReference? Work { get; init; }

    public PartyReference? Contributor { get; init; }

    public TimesheetsUiAction? UiAction { get; init; }
}
