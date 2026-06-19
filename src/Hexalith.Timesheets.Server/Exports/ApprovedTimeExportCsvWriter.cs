using System.Globalization;
using System.Text;

using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Server.Exports;

public static class ApprovedTimeExportCsvWriter
{
    public const string Header =
        "time_entry_id,contributor_party_id,target_kind,target_reference,service_date,duration_minutes,activity_type_id,activity_type_scope,billable_flag,approval_decision_id,approval_state,approval_scope,approver_party_id,approved_at_utc,row_state,approved_correction_id,correction_id,event_lineage,ai_availability,ai_wall_clock_ms,ai_model_runtime_ms,ai_billable_effort_minutes,ai_input_tokens,ai_output_tokens,ai_total_tokens,comment";

    public static string Write(IReadOnlyList<ApprovedTimeExportRowReadModel> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);

        StringBuilder builder = new();
        builder.Append(Header);

        foreach (ApprovedTimeExportRowReadModel row in rows)
        {
            builder.Append('\n');
            AppendRow(builder, row);
        }

        return builder.ToString();
    }

    private static void AppendRow(StringBuilder builder, ApprovedTimeExportRowReadModel row)
    {
        Append(builder, row.TimeEntryId.Value);
        Append(builder, row.Contributor.PartyId);
        Append(builder, row.Target.TargetKind.ToString());
        Append(builder, row.Target.TargetId);
        Append(builder, row.ServiceDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Append(builder, row.DurationMinutes.ToString(CultureInfo.InvariantCulture));
        Append(builder, row.ActivityTypeId.Value);
        Append(builder, row.ActivityTypeScope.ToString());
        Append(builder, row.BillableState.ToString());
        Append(builder, row.ApprovalDecision.TimeEntryApprovalDecisionId.Value);
        Append(builder, row.ApprovalDecision.ApprovalState.ToString());
        Append(builder, row.ApprovalDecision.ApprovalScope.ToString());
        Append(builder, row.ApprovalDecision.Approver.PartyId);
        Append(builder, row.ApprovalDecision.DecidedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        Append(builder, row.RowState.ToString());
        Append(builder, row.ApprovedCorrection?.TimeEntryCorrectionId.Value);
        Append(builder, row.Correction?.TimeEntryCorrectionId.Value);
        Append(builder, FormatLineage(row.EventLineage));
        Append(builder, row.AiMetrics?.Availability.ToString());
        Append(builder, row.AiMetrics?.WallClockDurationMilliseconds?.ToString(CultureInfo.InvariantCulture));
        Append(builder, row.AiMetrics?.ModelRuntimeMilliseconds?.ToString(CultureInfo.InvariantCulture));
        Append(builder, row.AiMetrics?.BillableEffortMinutes?.ToString(CultureInfo.InvariantCulture));
        Append(builder, row.AiMetrics?.ProviderInputTokenCount?.ToString(CultureInfo.InvariantCulture));
        Append(builder, row.AiMetrics?.ProviderOutputTokenCount?.ToString(CultureInfo.InvariantCulture));
        Append(builder, row.AiMetrics?.ProviderTotalTokenCount?.ToString(CultureInfo.InvariantCulture));
        AppendLast(builder, row.Comment?.Text);
    }

    private static string FormatLineage(IReadOnlyList<TimeEntryEventLineageItem> lineage)
        => string.Join(
            ';',
            lineage
                .OrderBy(static item => item.Ordinal)
                .Select(static item => string.Create(
                    CultureInfo.InvariantCulture,
                    $"{item.Ordinal}:{item.EventName}:{item.SourceAuthority}")));

    private static void Append(StringBuilder builder, string? value)
    {
        AppendLast(builder, value);
        builder.Append(',');
    }

    private static void AppendLast(StringBuilder builder, string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        bool mustQuote = value.Contains(',', StringComparison.Ordinal)
            || value.Contains('"', StringComparison.Ordinal)
            || value.Contains('\n', StringComparison.Ordinal)
            || value.Contains('\r', StringComparison.Ordinal);

        if (!mustQuote)
        {
            builder.Append(value);
            return;
        }

        builder.Append('"');
        builder.Append(value.Replace("\"", "\"\"", StringComparison.Ordinal));
        builder.Append('"');
    }
}
