namespace Hexalith.Timesheets.Projections.MagicLinks;

/// <summary>Invalid migration evidence requires abort; SDK/store availability failures remain retryable.</summary>
public sealed class MagicLinkV1MigrationValidationException(string message) : InvalidOperationException(message);
