using System.Text.Json.Serialization;

namespace Hexalith.Timesheets.Contracts.Policies;

[JsonConverter(typeof(JsonStringEnumConverter<TimesheetsEvidenceRetentionCategory>))]
public enum TimesheetsEvidenceRetentionCategory
{
    Unknown = 0,
    TimeEntryEvidence = 1,
    CommentText = 2,
    ExportRecord = 3,
    MagicLinkConfirmationAuditMetadata = 4
}

[JsonConverter(typeof(JsonStringEnumConverter<TimesheetsRetentionPosture>))]
public enum TimesheetsRetentionPosture
{
    Unknown = 0,
    RetainedByDefault = 1,
    ExcludedByDefault = 2,
    RedactedByDefault = 3,
    LegalHoldRequired = 4,
    TenantOverrideRequired = 5
}

[JsonConverter(typeof(JsonStringEnumConverter<TimesheetsCommentVisibilityScope>))]
public enum TimesheetsCommentVisibilityScope
{
    Unknown = 0,
    InternalDisplay = 1,
    ExternalConfirmationDisplay = 2,
    ProjectionReadModel = 3,
    ExportOutput = 4,
    SupportDiagnostics = 5
}

[JsonConverter(typeof(JsonStringEnumConverter<TimesheetsCommentPolicyDecision>))]
public enum TimesheetsCommentPolicyDecision
{
    Unknown = 0,
    Allowed = 1,
    Excluded = 2,
    Redacted = 3,
    PolicyRequired = 4
}

[JsonConverter(typeof(JsonStringEnumConverter<TimesheetsCommentRedactionRequirement>))]
public enum TimesheetsCommentRedactionRequirement
{
    Unknown = 0,
    NotRequired = 1,
    RequiredBeforeExternalDisclosure = 2,
    RequiredBeforeExport = 3
}
