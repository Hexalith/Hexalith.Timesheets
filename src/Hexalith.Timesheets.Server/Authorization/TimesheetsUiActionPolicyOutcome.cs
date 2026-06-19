namespace Hexalith.Timesheets.Server.Authorization;

public sealed record TimesheetsUiActionPolicyOutcome(
    TimesheetsUiAction Action,
    TimesheetsUiActionVisibility Visibility,
    string SafeMessage)
{
    /// <summary>
    /// Safe message emitted when authority cannot be resolved. Shared so consumers can
    /// detect the unresolved outcome without duplicating the literal text.
    /// </summary>
    public const string AuthorityUnresolvedMessage = "Authority cannot be resolved.";

    public static TimesheetsUiActionPolicyOutcome Allowed(TimesheetsUiAction action)
    {
        return new(action, TimesheetsUiActionVisibility.Allowed, "authorized");
    }

    public static TimesheetsUiActionPolicyOutcome Denied(
        TimesheetsUiAction action,
        TimesheetsUiActionVisibility visibility)
    {
        if (visibility == TimesheetsUiActionVisibility.Allowed)
        {
            throw new ArgumentOutOfRangeException(nameof(visibility), visibility, "Denied UI actions must be hidden or disabled.");
        }

        return new(action, visibility, "Access denied for this action.");
    }

    public static TimesheetsUiActionPolicyOutcome AuthorityUnresolved(
        TimesheetsUiAction action,
        TimesheetsUiActionVisibility visibility)
    {
        if (visibility == TimesheetsUiActionVisibility.Allowed)
        {
            throw new ArgumentOutOfRangeException(nameof(visibility), visibility, "Unresolved UI actions must be hidden or disabled.");
        }

        return new(action, visibility, AuthorityUnresolvedMessage);
    }

    public static TimesheetsUiActionPolicyOutcome FromDecision(
        TimesheetsUiAction action,
        TimesheetsAuthorizationDecision decision,
        TimesheetsUiActionVisibility deniedVisibility)
    {
        ArgumentNullException.ThrowIfNull(decision);

        if (decision.IsAuthorized)
        {
            return Allowed(action);
        }

        return IsAuthorityUnresolved(decision.DenialCategory)
            ? AuthorityUnresolved(action, deniedVisibility)
            : Denied(action, deniedVisibility);
    }

    private static bool IsAuthorityUnresolved(TimesheetsDenialCategory category)
    {
        return category is TimesheetsDenialCategory.MissingTenant
            or TimesheetsDenialCategory.UnknownUser
            or TimesheetsDenialCategory.StaleProjection
            or TimesheetsDenialCategory.AmbiguousAuthority
            or TimesheetsDenialCategory.UnavailableSiblingAuthority
            or TimesheetsDenialCategory.UnconfiguredPolicy
            or TimesheetsDenialCategory.CommentPolicyMissing
            or TimesheetsDenialCategory.RetentionPolicyMissing;
    }
}
