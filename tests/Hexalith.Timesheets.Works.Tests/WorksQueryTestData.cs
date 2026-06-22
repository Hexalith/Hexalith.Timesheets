using System.Text.Json;

using Hexalith.EventStore.Contracts.Queries;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Works.Contracts.Models;
using Hexalith.Works.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Works.Tests;

/// <summary>Shared builders for the Works reference-validation adapter tests.</summary>
internal static class WorksQueryTestData
{
    public const string TenantValue = "tenant-a";
    public const string WorkIdValue = "work-123";

    // Mirrors the serialization the real GetWorkItemQueryHandler uses to project WorkItemView.
    private static readonly JsonSerializerOptions s_webOptions = new(JsonSerializerDefaults.Web);

    public static TimesheetsRequestContext Context()
        => new(new TenantReference(TenantValue), new PartyReference("actor-1"), "corr-1");

    public static WorkReference Work() => new(WorkIdValue);

    public static WorkItemView FoundView(
        WorkItemStatus status,
        string tenant = TenantValue,
        long sourceSequence = 7,
        decimal? estimated = null,
        decimal? done = null,
        decimal? remaining = null,
        string? unit = null)
        => new(
            new TenantId(tenant),
            new WorkItemId(WorkIdValue),
            true,
            status,
            estimated,
            done,
            remaining,
            unit is null ? null : new Unit(unit),
            null,
            sourceSequence);

    public static WorkItemView NotFoundView()
        => WorkItemView.NotFound(new TenantId(TenantValue), new WorkItemId(WorkIdValue));

    /// <summary>Wraps a view exactly as the Works query handler would: a successful payload result.</summary>
    public static QueryResult SuccessResult(WorkItemView view)
        => QueryResult.FromPayload(JsonSerializer.SerializeToElement(view, s_webOptions), "work-item-view");
}
