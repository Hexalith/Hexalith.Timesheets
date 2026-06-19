namespace Hexalith.Timesheets.Contracts.Commands.MagicLinks;

/// <summary>
/// Capability-scoped confirmation request. The body carries no authority or attribution fields: the server
/// derives all of those from the validated capability state, so an external caller cannot influence what is
/// attributed or persisted.
/// </summary>
public sealed record ConfirmTimeThroughMagicLink();
