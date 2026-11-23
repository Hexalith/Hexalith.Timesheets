using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.ApprovedTimeLedger;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.Exports;

using Shouldly;

namespace Hexalith.Timesheets.Server.Tests;

public sealed partial class ApprovedTimeExportServiceTests
{
    [Theory]
    [InlineData("checkpoint-drift")]
    [InlineData("snapshot-drift")]
    [InlineData("as-of-drift")]
    [InlineData("uncertified-membership")]
    [InlineData("unknown-checkpoint")]
    [InlineData("duplicate-current")]
    [InlineData("stale-page")]
    [InlineData("stale-row")]
    [InlineData("row-checkpoint-mismatch")]
    [InlineData("row-as-of-mismatch")]
    [InlineData("out-of-scope")]
    [InlineData("stalled")]
    [InlineData("cycle")]
    [InlineData("empty-reader-veto")]
    [InlineData("reader-unavailable")]
    [InlineData("lost-access")]
    [InlineData("lost-export-authority")]
    [InlineData("lost-resource-on-page")]
    [InlineData("lost-prior-resource")]
    [InlineData("browsing-cursor")]
    public async Task InvalidTraversalWithholdsPreviewReadinessAndEveryGeneratedRow(string fault)
    {
        ApprovedTimeLedgerRowReadModel firstRow = Row("entry-1", ProjectTarget());
        ApprovedTimeLedgerReadModel first = Page([firstRow]) with { NextCursor = "next" };
        ApprovedTimeLedgerReadModel second = Page([Row("entry-2", ProjectTarget())]);
        QueryApprovedTimeLedger filters = Command().LedgerQuery with { Project = new("project-1") };
        List<ApprovedTimeLedgerReadModel?> pages = [first, second];
        switch (fault)
        {
            case "checkpoint-drift":
                pages[1] = second with
                {
                    ProjectionFreshness = first.ProjectionFreshness with { Cursor = "101" }
                };
                break;
            case "snapshot-drift":
                pages[1] = second with { ExportSnapshotId = "another-snapshot" };
                break;
            case "as-of-drift":
                pages[0] = first with { ProjectionFreshness = first.ProjectionFreshness with { AsOfUtc = RequestedAtUtc() } };
                pages[1] = second with { ProjectionFreshness = second.ProjectionFreshness with { AsOfUtc = RequestedAtUtc().AddMinutes(1) } };
                break;
            case "unknown-checkpoint":
                pages[1] = second with { ExportSnapshotId = null };
                break;
            case "uncertified-membership":
                // Independent owners retain the same local checkpoint when another entry joins the result.
                pages[0] = first with { ExportSnapshotId = null };
                pages[1] = second with { ExportSnapshotId = null, Items = [Row("new-owner", ProjectTarget())] };
                break;
            case "duplicate-current":
                pages[1] = Page([firstRow with { DurationMinutes = 90 }]);
                break;
            case "stale-page":
                pages[1] = second with { ProjectionFreshness = ProjectionFreshnessMetadata.Stale() };
                break;
            case "stale-row":
                pages[1] = Page([second.Items[0] with { ProjectionFreshness = ProjectionFreshnessMetadata.Stale() }]);
                break;
            case "row-checkpoint-mismatch":
                pages[1] = Page([second.Items[0] with { ProjectionFreshness = second.ProjectionFreshness with { Cursor = "101" } }]);
                break;
            case "row-as-of-mismatch":
                var snapshot = second.ProjectionFreshness with { AsOfUtc = RequestedAtUtc() };
                pages[0] = first with { ProjectionFreshness = snapshot };
                pages[1] = second with
                {
                    ProjectionFreshness = snapshot,
                    Items = [second.Items[0] with { ProjectionFreshness = snapshot with { AsOfUtc = RequestedAtUtc().AddMinutes(1) } }]
                };
                break;
            case "out-of-scope":
                pages[1] = Page([Row("entry-2", WorkTarget())]);
                break;
            case "stalled":
                pages[1] = second with { NextCursor = "next" };
                break;
            case "cycle":
                pages[1] = Page([]) with { NextCursor = "third" };
                pages.Add(Page([]) with { NextCursor = "next" });
                break;
            case "empty-reader-veto":
                pages[1] = Page([]) with { NextCursor = "third", CanUseForExport = false };
                pages.Add(second);
                break;
            case "reader-unavailable":
                pages[1] = null;
                break;
            case "browsing-cursor":
                filters = filters with { Cursor = "next" };
                break;
        }

        foreach (bool preview in new[] { true, false })
        {
            int approvalsBeforeLoss = fault switch
            {
                "lost-access" => 3,
                "lost-resource-on-page" => 4,
                "lost-export-authority" => 5,
                "lost-prior-resource" => 6,
                _ => 0
            };
            bool lostAccess = approvalsBeforeLoss > 0;
            TimesheetsDenialCategory denial = fault is "lost-resource-on-page" or "lost-prior-resource"
                ? TimesheetsDenialCategory.InsufficientRole : TimesheetsDenialCategory.NonMember;
            TimesheetsAuthorizationDecision[] decisions = lostAccess
                ? [.. Enumerable.Repeat(TimesheetsAuthorizationDecision.Allowed(), approvalsBeforeLoss),
                    TimesheetsAuthorizationDecision.Denied(denial, "Access changed.")]
                : [];
            SequentialPagedLedgerReader reader = new(pages);
            TrackingAuditRecorder recorder = new();
            ApprovedTimeExportService service = Service(new SequencedAccessGuard(decisions), reader, new TrackingHydrator(), recorder);
            CancellationToken token = TestContext.Current.CancellationToken;
            if (preview)
            {
                ApprovedTimePreviewResult result = await service.PreviewAsync(Context(), new() { LedgerQuery = filters }, RequestedAtUtc(), token);
                if (lostAccess)
                {
                    result.WasDisclosed.ShouldBeFalse();
                    result.Preview.ShouldBeNull();
                }
                else
                {
                    result.Preview.ShouldNotBeNull().Readiness.ShouldBe(ApprovedTimeExportReadinessState.Blocked);
                }
            }
            else
            {
                ApprovedTimeExportResult result = await service.GenerateAsync(Context(), new() { LedgerQuery = filters }, RequestedAtUtc(), token);
                if (lostAccess)
                {
                    result.WasGenerated.ShouldBeFalse();
                    result.Export.ShouldBeNull();
                }
                else
                {
                    AssertNoDelivery(result);
                }
            }

            if (fault == "browsing-cursor")
            {
                reader.Calls.ShouldBe(0);
            }
            if (fault is "empty-reader-veto" or "reader-unavailable")
            {
                reader.Calls.ShouldBe(2);
            }
            recorder.Events.ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task SingleAtomicPageCanOmitWholeScopeSnapshotIdentity()
    {
        var page = Page([Row("entry", ProjectTarget())]) with { ExportSnapshotId = null };
        var service = Service(new SequencedAccessGuard([]), new TrackingLedgerReader(page), new TrackingHydrator());
        var result = await service.GenerateAsync(Context(), Command(), RequestedAtUtc(), TestContext.Current.CancellationToken);
        result.WasGenerated.ShouldBeTrue();
        result.Export.ShouldNotBeNull().Scope.LedgerCheckpoint.ShouldBeNull();
    }

    [Fact]
    public async Task FreshNonemptyReaderVetoIsReportedByQueryPreviewAndGeneration()
    {
        ApprovedTimeLedgerReadModel page = Page([Row("entry", ProjectTarget())]) with
        {
            CanUseForExport = false,
            ExportReadinessDetail = "untrusted-reader-marker"
        };
        var guard = new SequencedAccessGuard([]);
        var ledger = new ApprovedTimeLedgerQueryService(guard, new TrackingLedgerReader(page), new TrackingHydrator());
        CancellationToken token = TestContext.Current.CancellationToken;
        var disclosed = (await ledger.QueryAsync(Context(), Command().LedgerQuery, token)).Page.ShouldNotBeNull();
        disclosed.Items.Count.ShouldBe(1);
        disclosed.CanUseForExport.ShouldBeFalse();
        disclosed.ExportReadinessDetail.ShouldBe("The ledger reader has withheld these rows from export.");
        TrackingAuditRecorder recorder = new();
        ApprovedTimeExportService service = new(guard, ledger, recorder, FinanceExportTestPolicy.Configured);
        var preview = (await service.PreviewAsync(Context(), new(), RequestedAtUtc(), token)).Preview.ShouldNotBeNull();
        preview.Readiness.ShouldBe(ApprovedTimeExportReadinessState.Blocked);
        preview.ReadinessDetail.ShouldBe(disclosed.ExportReadinessDetail);
        ApprovedTimeExportResult result = await service.GenerateAsync(Context(), Command(), RequestedAtUtc(), token);
        AssertNoDelivery(result);
        result.Export!.ReadinessDetail.ShouldBe(disclosed.ExportReadinessDetail);
        recorder.Events.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task TraversalCrossesEmptyAuthorizedPageAndKeepsBothLineageRows(bool currentOnly, bool includeSuperseded)
    {
        ApprovedTimeLedgerRowReadModel current = Row("entry", ProjectTarget());
        ProjectionFreshnessMetadata snapshot = ProjectionFreshnessMetadata.Fresh with { Cursor = "100", AsOfUtc = RequestedAtUtc() };
        ApprovedTimeLedgerReadModel[] pages =
        [
            Page([]) with { NextCursor = "second", ProjectionFreshness = snapshot },
            Page([current]) with { NextCursor = "third", ProjectionFreshness = snapshot },
            Page([current with { RowState = ApprovedTimeLedgerRowState.Superseded }]) with { ProjectionFreshness = snapshot }
        ];
        QueryApprovedTimeLedger filters = Command().LedgerQuery with
        {
            CurrentRowsOnly = currentOnly,
            IncludeSupersededRows = includeSuperseded
        };
        CancellationToken token = TestContext.Current.CancellationToken;
        SequentialPagedLedgerReader previewReader = new(pages);
        TrackingAuditRecorder recorder = new();
        ApprovedTimeExportService previewService = Service(new SequencedAccessGuard([]), previewReader, new TrackingHydrator(), recorder);
        ApprovedTimeExportPreviewReadModel preview = (await previewService.PreviewAsync(Context(), new() { LedgerQuery = filters },
            RequestedAtUtc(), token)).Preview.ShouldNotBeNull();
        preview.Readiness.ShouldBe(ApprovedTimeExportReadinessState.Ready);
        preview.Scope.RowCount.ShouldBe(2);
        preview.Scope.IncludesSupersededRows.ShouldBeTrue();
        preview.Scope.CurrentRowsOnly.ShouldBeFalse();
        preview.Scope.Filters.ShouldBe(filters);
        previewReader.ObservedCursors.ShouldBe([null, "second", "third"]);
        recorder.Events.ShouldBeEmpty();

        SequentialPagedLedgerReader exportReader = new(pages);
        ApprovedTimeExportService generateService = Service(new SequencedAccessGuard([]), exportReader, new TrackingHydrator(), recorder);
        ApprovedTimeExportResult result = await generateService.GenerateAsync(Context(), new() { LedgerQuery = filters }, RequestedAtUtc(), token);
        result.WasGenerated.ShouldBeTrue();
        result.Export.ShouldNotBeNull().Rows.Select(row => row.RowState)
            .ShouldBe([ApprovedTimeLedgerRowState.Current, ApprovedTimeLedgerRowState.Superseded]);
        exportReader.ObservedCursors.ShouldBe(previewReader.ObservedCursors);
        var receipt = recorder.Events.ShouldHaveSingleItem();
        receipt.Filters.Cursor.ShouldBeNull();
        receipt.OutputScope.ShouldBe(preview.Scope);
        receipt.OutputScope.LedgerCheckpoint.ShouldBe("fixture-snapshot");
        receipt.OutputScope.LedgerAsOfUtc.ShouldBe(RequestedAtUtc());
        result.Export.Scope.ShouldBe(receipt.OutputScope);
        result.Export.Audit.OutputScope.ShouldBe(receipt.OutputScope);
    }
}
