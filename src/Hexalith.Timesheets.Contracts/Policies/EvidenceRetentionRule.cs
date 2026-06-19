namespace Hexalith.Timesheets.Contracts.Policies;

public sealed record EvidenceRetentionRule(
    TimesheetsEvidenceRetentionCategory Category,
    TimesheetsRetentionPosture Posture,
    string SafeGuidance);
