using System.Text.Json;

using Hexalith.Timesheets.Contracts.Commands.Exports;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Policies;
using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections;
using Hexalith.Timesheets.Projections.ApprovedTimeLedger;
using Hexalith.Timesheets.Projections.TimeEntries;
using Hexalith.Timesheets.Server.ApprovedTimeLedger;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.Exports;
using Hexalith.Timesheets.Server.TimeEntries;
using Hexalith.Timesheets.Server.Policies;
using Hexalith.Timesheets.Server.References;

using NSubstitute;

using Shouldly;

namespace Hexalith.Timesheets.IntegrationTests;

public sealed partial class ApprovedTimeExportIntegrationTests
{
    [Fact]
    public void GoldenCsvV1FixturePinsProjectWorkBoundariesLineageAiMetricsAndCommentSafety()
    {
        string actual = ApprovedTimeExportCsvWriter.Write([
            GoldenRow(
                "project-entry",
                TimeEntryTargetReference.ForProject(Project()),
                new PartyReference("party-1"),
                new ActivityTypeId("activity-type-1"),
                ActivityTypeScope.Tenant,
                new DateOnly(2026, 3, 29),
                30,
                "approver-1",
                new DateTimeOffset(2026, 3, 29, 21, 30, 0, TimeSpan.Zero),
                ApprovedTimeLedgerRowState.Current,
                [
                    new("TimeEntryRecorded", 1, TimeEntryEvidenceSourceAuthority.TimesheetsDomainEvents),
                    new("TimeEntryApproved", 2, TimeEntryEvidenceSourceAuthority.TimesheetsDomainEvents)
                ]) with
            {
                AiMetrics = AiEffortMetrics.Unavailable
            },
            GoldenRow(
                "work-entry",
                TimeEntryTargetReference.ForWork(Work()),
                new PartyReference("party-2"),
                new ActivityTypeId("activity-type-2"),
                ActivityTypeScope.Project,
                new DateOnly(2026, 10, 25),
                45,
                "approver-2",
                new DateTimeOffset(2026, 10, 25, 22, 30, 0, TimeSpan.Zero),
                ApprovedTimeLedgerRowState.Superseded,
                [
                    new("TimeEntryRecorded", 1, TimeEntryEvidenceSourceAuthority.TimesheetsDomainEvents),
                    new("TimeEntryRejected", 2, TimeEntryEvidenceSourceAuthority.TimesheetsDomainEvents),
                    new("TimeEntryCorrected", 3, TimeEntryEvidenceSourceAuthority.TimesheetsDomainEvents),
                    new("TimeEntryApproved", 4, TimeEntryEvidenceSourceAuthority.TimesheetsDomainEvents)
                ]) with
            {
                ApprovedCorrection = GoldenApprovedCorrection("work-entry"),
                Correction = GoldenRejectedCorrection("work-entry"),
                AiMetrics = new(
                    AiMetricAvailability.ProviderReported,
                    1200,
                    900,
                    15,
                    100,
                    50,
                    150),
                CommentExportState = TimesheetsCommentPolicyDecision.Allowed,
                Comment = AllowedExportComment("=SUM(A1:A2)")
            }
        ]);
        string expected = File.ReadAllText(TestRepositoryRoot.PathTo(
            "tests",
            "Hexalith.Timesheets.IntegrationTests",
            "Exports",
            "Golden",
            "approved-time-export-v1-project-work-boundaries.csv")).TrimEnd('\n');

        actual.ShouldBe(expected);
        actual.ShouldNotContain("\r\n");
        actual.ShouldContain("'=SUM(A1:A2)");
        actual.ShouldNotContain("Comment that must not be exported");
    }

    [Fact]
    public async Task SeededApprovedBillableProjectAndWorkRowsExportWithFreshnessLineageAndNoEmptyFile()
    {
        TimeEntryProjectionEvent[] events =
        [
            Event("m1", 1, Recorded("time-entry-1", TimeEntryTargetReference.ForProject(Project()), 30)),
            Event("m2", 2, Submitted("time-entry-1")),
            Event("m3", 3, Approved("time-entry-1", "decision-1")),
            Event("m4", 4, Recorded("time-entry-2", TimeEntryTargetReference.ForWork(Work()), 45)),
            Event("m5", 5, Submitted("time-entry-2")),
            Event("m6", 6, Approved("time-entry-2", "decision-2")),
            Event("m7", 7, ApprovedCorrected("time-entry-2", TimeEntryTargetReference.ForWork(Work()), 60))
        ];
        ApprovedTimeLedgerProjection ledgerProjection = new();
        TimesheetsProjectionCheckpoint checkpoint = new(
            "tenant-1",
            ApprovedTimeLedgerProjection.ProjectionName,
            7,
            ProjectionFreshness.Fresh);
        ApprovedTimeExportService service = new(
            new AllowAllAccessGuard(),
            new ApprovedTimeLedgerQueryService(
                new AllowAllAccessGuard(),
                new FrozenLedgerTestReader(ledgerProjection, events, checkpoint),
                new StaticHydrator()),
            new AcknowledgingExportTestRecorder(),
            FinanceExportTestPolicy.Configured);

        GenerateApprovedTimeExport command = new()
        {
            LedgerQuery = new QueryApprovedTimeLedger
            {
                BillableState = BillableState.Billable,
                CurrentRowsOnly = false,
                IncludeSupersededRows = true,
                SortBy = TimeEntryQuerySortBy.TimeEntryId,
                PageSize = 1
            }
        };

        ApprovedTimeExportResult result = await service.GenerateAsync(
            Context(),
            command,
            new DateTimeOffset(2026, 6, 19, 15, 0, 0, TimeSpan.Zero),
            TestContext.Current.CancellationToken);

        result.WasGenerated.ShouldBeTrue();
        ApprovedTimeExportReadModel export = result.Export.ShouldNotBeNull();
        export.ProjectionFreshness.State.ShouldBe(ProjectionFreshnessState.Fresh);
        export.Audit.Requester.ShouldBe(new PartyReference("operator-1"));
        export.Audit.CorrelationId.ShouldBe("correlation-1");
        export.Scope.RowCount.ShouldBe(3);
        export.Rows.Select(static row => row.TimeEntryId.Value).ShouldBe(["time-entry-1", "time-entry-2", "time-entry-2"]);
        export.Rows.Count(static row => row.RowState == ApprovedTimeLedgerRowState.Superseded).ShouldBe(1);
        export.Rows.Any(static row => row.ApprovedCorrection is not null).ShouldBeTrue();
        export.CsvContent.ShouldNotBeNull().ShouldContain("time-entry-1");
        export.CsvContent.ShouldNotBeNull().ShouldContain("time-entry-2");
        export.CsvContent.ShouldNotBeNull().ShouldContain("TimeEntryApproved");

        var singlePage = await service.GenerateAsync(
            Context(), command with { LedgerQuery = command.LedgerQuery with { PageSize = 50 } },
            export.Audit.RequestedAtUtc, TestContext.Current.CancellationToken);
        singlePage.WasGenerated.ShouldBeTrue();
        singlePage.Export.ShouldNotBeNull().CsvContent.ShouldBe(export.CsvContent);
        JsonSerializer.Serialize(singlePage.Export.Rows).ShouldBe(JsonSerializer.Serialize(export.Rows));

        GenerateApprovedTimeExport noResultsCommand = command with
        {
            LedgerQuery = command.LedgerQuery with
            {
                Contributor = new PartyReference("missing-party")
            }
        };

        ApprovedTimeExportResult noResults = await service.GenerateAsync(
            Context(),
            noResultsCommand,
            new DateTimeOffset(2026, 6, 19, 15, 0, 0, TimeSpan.Zero),
            TestContext.Current.CancellationToken);

        noResults.WasGenerated.ShouldBeFalse();
        ApprovedTimeExportReadModel blocked = noResults.Export.ShouldNotBeNull();
        blocked.Readiness.ShouldBe(ApprovedTimeExportReadinessState.Blocked);
        blocked.CsvContent.ShouldBeNull();
        blocked.Audit.BlockedReason.ShouldBe("No approved ledger rows are available for export preview.");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CorrectionBetweenProjectionReadsWithholdsFinanceOutput(bool preview)
    {
        var target = TimeEntryTargetReference.ForProject(Project());
        List<TimeEntryProjectionEvent> history =
        [
            Event("m1", 1, Recorded("time-entry-1", target, 30)),
            Event("m2", 2, Submitted("time-entry-1")),
            Event("m3", 3, Approved("time-entry-1", "decision-1")),
            Event("m4", 4, Recorded("time-entry-2", target, 45)),
            Event("m5", 5, Submitted("time-entry-2")),
            Event("m6", 6, Approved("time-entry-2", "decision-2"))
        ];
        ApprovedTimeLedgerProjection projection = new();
        var checkpoint = new TimesheetsProjectionCheckpoint("tenant-1", ApprovedTimeLedgerProjection.ProjectionName, 6, ProjectionFreshness.Fresh);
        var reader = Substitute.For<IApprovedTimeLedgerProjectionReader>();
        List<ApprovedTimeLedgerReadModel> pages = [];
        reader.QueryAsync(Arg.Any<TimesheetsRequestContext>(), Arg.Any<QueryApprovedTimeLedger>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var filters = call.Arg<QueryApprovedTimeLedger>();
                var page = projection.Project("tenant-1", history, checkpoint, filters) with
                {
                    ExportSnapshotId = pages.Count == 0 ? "before-correction" : "after-correction"
                };
                if (pages.Count == 0)
                {
                    history.Add(Event("m7", 7, ApprovedCorrected("time-entry-2", target, 60)));
                }
                pages.Add(page);
                return ValueTask.FromResult<ApprovedTimeLedgerReadModel?>(page);
            });
        var guard = new AllowAllAccessGuard();
        var audit = Substitute.For<IApprovedTimeExportAuditRecorder>();
        var service = new ApprovedTimeExportService(guard, new(guard, reader, new StaticHydrator()), audit, FinanceExportTestPolicy.Configured);
        var query = new QueryApprovedTimeLedger { BillableState = BillableState.Billable, IncludeSupersededRows = true, PageSize = 1 };
        var token = TestContext.Current.CancellationToken;
        if (preview)
        {
            var result = await service.PreviewAsync(Context(), new() { LedgerQuery = query }, DateTimeOffset.UnixEpoch, token);
            result.Preview.ShouldNotBeNull().Readiness.ShouldBe(ApprovedTimeExportReadinessState.Blocked);
            result.Preview.Scope.RowCount.ShouldBe(0);
            result.Preview.ReadinessDetail.ShouldContain("Retry the export");
        }
        else
        {
            var result = await service.GenerateAsync(Context(), new() { LedgerQuery = query }, DateTimeOffset.UnixEpoch, token);
            AssertWithheld(result);
            result.Export!.ReadinessDetail.ShouldContain("Retry the export");
        }

        pages.Count.ShouldBe(2);
        pages[0].Items.ShouldHaveSingleItem().TimeEntryId.Value.ShouldBe("time-entry-1");
        pages[1].Items.ShouldHaveSingleItem().DurationMinutes.ShouldBe(60);
        pages[1].NextCursor.ShouldNotBeNull();
        audit.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public async Task ExportScopeRespectsWorkContributorActivityPeriodDateAndBillableFilters()
    {
        TimeEntryProjectionEvent[] events =
        [
            Event("m1", 1, Recorded("project-entry", TimeEntryTargetReference.ForProject(Project()), 30)),
            Event("m2", 2, Submitted("project-entry")),
            Event("m3", 3, Approved("project-entry", "decision-1")),
            Event(
                "m4",
                4,
                Recorded(
                    "matching-work-entry",
                    TimeEntryTargetReference.ForWork(Work()),
                    45,
                    new PartyReference("party-2"),
                    new ActivityTypeId("activity-type-2"),
                    new DateOnly(2026, 7, 15),
                    BillableState.Billable)),
            Event("m5", 5, Submitted("matching-work-entry")),
            Event("m6", 6, Approved("matching-work-entry", "decision-2")),
            Event(
                "m7",
                7,
                Recorded(
                    "wrong-contributor-entry",
                    TimeEntryTargetReference.ForWork(Work()),
                    60,
                    new PartyReference("party-3"),
                    new ActivityTypeId("activity-type-2"),
                    new DateOnly(2026, 7, 16),
                    BillableState.Billable)),
            Event("m8", 8, Submitted("wrong-contributor-entry")),
            Event("m9", 9, Approved("wrong-contributor-entry", "decision-3")),
            Event(
                "m10",
                10,
                Recorded(
                    "wrong-activity-entry",
                    TimeEntryTargetReference.ForWork(Work()),
                    75,
                    new PartyReference("party-2"),
                    new ActivityTypeId("activity-type-3"),
                    new DateOnly(2026, 7, 17),
                    BillableState.Billable)),
            Event("m11", 11, Submitted("wrong-activity-entry")),
            Event("m12", 12, Approved("wrong-activity-entry", "decision-4")),
            Event(
                "m13",
                13,
                Recorded(
                    "non-billable-entry",
                    TimeEntryTargetReference.ForWork(Work()),
                    90,
                    new PartyReference("party-2"),
                    new ActivityTypeId("activity-type-2"),
                    new DateOnly(2026, 7, 18),
                    BillableState.NonBillable)),
            Event("m14", 14, Submitted("non-billable-entry")),
            Event("m15", 15, Approved("non-billable-entry", "decision-5")),
            Event(
                "m16",
                16,
                Recorded(
                    "out-of-period-entry",
                    TimeEntryTargetReference.ForWork(Work()),
                    105,
                    new PartyReference("party-2"),
                    new ActivityTypeId("activity-type-2"),
                    new DateOnly(2026, 8, 1),
                    BillableState.Billable)),
            Event("m17", 17, Submitted("out-of-period-entry")),
            Event("m18", 18, Approved("out-of-period-entry", "decision-6"))
        ];
        ApprovedTimeLedgerProjection ledgerProjection = new();
        ApprovedTimeExportService service = new(
            new AllowAllAccessGuard(),
            new ApprovedTimeLedgerQueryService(
                new AllowAllAccessGuard(),
                new FrozenLedgerTestReader(
                    ledgerProjection,
                    events,
                    new("tenant-1", ApprovedTimeLedgerProjection.ProjectionName, 18, ProjectionFreshness.Fresh)),
                new StaticHydrator()),
            new AcknowledgingExportTestRecorder(),
            FinanceExportTestPolicy.Configured);

        GenerateApprovedTimeExport command = new()
        {
            LedgerQuery = new QueryApprovedTimeLedger
            {
                Work = Work(),
                Contributor = new PartyReference("party-2"),
                ActivityTypeId = new ActivityTypeId("activity-type-2"),
                TenantLocalPeriodKey = "2026-07",
                ServiceDateFrom = new DateOnly(2026, 7, 1),
                ServiceDateTo = new DateOnly(2026, 7, 31),
                BillableState = BillableState.Billable,
                SortBy = TimeEntryQuerySortBy.TimeEntryId,
                PageSize = 50
            }
        };

        ApprovedTimeExportResult result = await service.GenerateAsync(
            Context(),
            command,
            new DateTimeOffset(2026, 6, 19, 15, 0, 0, TimeSpan.Zero),
            TestContext.Current.CancellationToken);

        result.WasGenerated.ShouldBeTrue();
        ApprovedTimeExportReadModel export = result.Export.ShouldNotBeNull();
        ApprovedTimeExportRowReadModel row = export.Rows.ShouldHaveSingleItem();
        row.TimeEntryId.ShouldBe(new TimeEntryId("matching-work-entry"));
        row.Target.TargetKind.ShouldBe(TimeEntryTargetKind.Work);
        row.Target.TargetId.ShouldBe("work-1");
        row.Contributor.ShouldBe(new PartyReference("party-2"));
        row.ActivityTypeId.ShouldBe(new ActivityTypeId("activity-type-2"));
        row.ServiceDate.ShouldBe(new DateOnly(2026, 7, 15));
        row.BillableState.ShouldBe(BillableState.Billable);
        export.Scope.Filters.Work.ShouldBe(Work());
        export.Audit.Filters.TenantLocalPeriodKey.ShouldBe("2026-07");
        export.CsvContent.ShouldNotBeNull().ShouldContain("matching-work-entry");
        export.CsvContent.ShouldNotBeNull().ShouldNotContain("project-entry");
        export.CsvContent.ShouldNotBeNull().ShouldNotContain("wrong-contributor-entry");
        export.CsvContent.ShouldNotBeNull().ShouldNotContain("wrong-activity-entry");
        export.CsvContent.ShouldNotBeNull().ShouldNotContain("non-billable-entry");
        export.CsvContent.ShouldNotBeNull().ShouldNotContain("out-of-period-entry");
    }

    [Fact]
    public async Task ExportTenantLocalPeriodKeyFiltersIndependentlyOfUtcAuditInstants()
    {
        // Both entries are approved at the same June UTC instant. The period key keys off the
        // tenant-local service date only, so "2026-03" must include the March-service row and
        // exclude the April-service row even though the UTC approval instant is identical and
        // sits in a different month. No ServiceDateFrom/To is supplied, so the tenant-local
        // period key is the sole discriminator (NFR15 / AC4).
        TimeEntryProjectionEvent[] events =
        [
            Event(
                "m1",
                1,
                Recorded(
                    "march-service-entry",
                    TimeEntryTargetReference.ForWork(Work()),
                    30,
                    new PartyReference("party-1"),
                    new ActivityTypeId("activity-type-1"),
                    new DateOnly(2026, 3, 31),
                    BillableState.Billable)),
            Event("m2", 2, Submitted("march-service-entry")),
            Event("m3", 3, Approved("march-service-entry", "decision-1")),
            Event(
                "m4",
                4,
                Recorded(
                    "april-service-entry",
                    TimeEntryTargetReference.ForWork(Work()),
                    30,
                    new PartyReference("party-1"),
                    new ActivityTypeId("activity-type-1"),
                    new DateOnly(2026, 4, 1),
                    BillableState.Billable)),
            Event("m5", 5, Submitted("april-service-entry")),
            Event("m6", 6, Approved("april-service-entry", "decision-2"))
        ];
        ApprovedTimeLedgerProjection ledgerProjection = new();
        ApprovedTimeExportService service = new(
            new AllowAllAccessGuard(),
            new ApprovedTimeLedgerQueryService(
                new AllowAllAccessGuard(),
                new FrozenLedgerTestReader(
                    ledgerProjection,
                    events,
                    new("tenant-1", ApprovedTimeLedgerProjection.ProjectionName, 6, ProjectionFreshness.Fresh)),
                new StaticHydrator()),
            new AcknowledgingExportTestRecorder(),
            FinanceExportTestPolicy.Configured);

        GenerateApprovedTimeExport command = new()
        {
            LedgerQuery = new QueryApprovedTimeLedger
            {
                Work = Work(),
                TenantLocalPeriodKey = "2026-03",
                BillableState = BillableState.Billable,
                SortBy = TimeEntryQuerySortBy.TimeEntryId,
                PageSize = 50
            }
        };

        ApprovedTimeExportResult result = await service.GenerateAsync(
            Context(),
            command,
            new DateTimeOffset(2026, 6, 19, 15, 0, 0, TimeSpan.Zero),
            TestContext.Current.CancellationToken);

        result.WasGenerated.ShouldBeTrue();
        ApprovedTimeExportReadModel export = result.Export.ShouldNotBeNull();
        ApprovedTimeExportRowReadModel row = export.Rows.ShouldHaveSingleItem();
        row.TimeEntryId.ShouldBe(new TimeEntryId("march-service-entry"));
        row.ServiceDate.ShouldBe(new DateOnly(2026, 3, 31));

        // The period key alone did the filtering: no UTC date range was supplied.
        export.Audit.Filters.TenantLocalPeriodKey.ShouldBe("2026-03");
        export.Audit.Filters.ServiceDateFrom.ShouldBeNull();
        export.Audit.Filters.ServiceDateTo.ShouldBeNull();

        // Tenant-local period key (March) stays separate from the UTC audit instant (June).
        export.CsvContent.ShouldNotBeNull().ShouldContain("2026-03-31");
        export.CsvContent.ShouldNotBeNull().ShouldContain("2026-06-19T13:00:00.0000000+00:00");
        export.CsvContent.ShouldNotBeNull().ShouldNotContain("april-service-entry");
    }

    [Theory]
    [InlineData(1, true, false)]
    [InlineData(2, true, false)]
    [InlineData(2, true, true)]
    [InlineData(0, true, false)]
    [InlineData(99, true, false)]
    [InlineData(2, false, true)]
    public async Task PublicationAndFinanceDecisionsSurviveTheFullCorrectionJourney(
        int correctedVersion, bool hostAllows, bool financeCleared)
    {
        const string Id = "corrected-entry";
        TimeEntryTargetReference target = TimeEntryTargetReference.ForProject(Project());
        TimeEntryComment original = new("original-internal-marker", TimeEntryCommentPolicy.SensitiveDefault with
        {
            ServerPublicationVersion = 1,
            ServerProjectionInclusion = TimesheetsCommentPolicyDecision.Allowed,
            ExportInclusion = TimesheetsCommentPolicyDecision.Excluded
        });
        TimeEntryComment corrected = new("corrected-export-marker", original.Policy with
        {
            ServerPublicationVersion = correctedVersion,
            ExportInclusion = TimesheetsCommentPolicyDecision.Allowed,
            ServerExportInclusion = financeCleared ? TimesheetsCommentPolicyDecision.Allowed : null
        });
        TimeEntryApprovedCorrected correction = ApprovedCorrected(Id, target, 60);
        TimeEntryProjectionEvent[] events =
        [
            Event("m1", 1, Recorded(Id, target, 45) with { Comment = original }),
            Event("m2", 2, Submitted(Id)),
            Event("m3", 3, Approved(Id, "decision-2")),
            Event("m4", 4, correction with
            {
                PreviousValues = correction.PreviousValues with { Comment = original },
                CorrectedValues = correction.CorrectedValues with { Comment = corrected }
            })
        ];
        AllowAllAccessGuard guard = new();
        var options = FinanceExportTestPolicy.Configured with { ProjectionCommentsAllowed = hostAllows };
        var projection = new ApprovedTimeLedgerProjection(options);
        var checkpoint = new TimesheetsProjectionCheckpoint("tenant-1", ApprovedTimeLedgerProjection.ProjectionName,
            4, ProjectionFreshness.Fresh);
        ApprovedTimeLedgerQueryService ledger = new(guard, new FrozenLedgerTestReader(projection, events, checkpoint),
            new StaticHydrator(), options);
        QueryApprovedTimeLedger query = new() { BillableState = BillableState.Billable, IncludeSupersededRows = true, PageSize = 1 };
        CancellationToken token = TestContext.Current.CancellationToken;
        var fullQuery = query with { PageSize = 2 };
        var rebuilt = await ledger.QueryAsync(Context(), fullQuery, token);
        string ledgerJson = JsonSerializer.Serialize(rebuilt.Page.ShouldNotBeNull().Items);
        bool currentVisible = hostAllows && correctedVersion is 1 or 2;
        bool currentExported = currentVisible && financeCleared;
        ledgerJson.Contains(original.Text, StringComparison.Ordinal).ShouldBe(hostAllows);
        ledgerJson.Contains(corrected.Text, StringComparison.Ordinal).ShouldBe(currentVisible);
        rebuilt.Page.Items.Select(row => row.TimeEntryId.Value).ShouldBe([Id, Id]);
        rebuilt.Page.Items.ShouldAllBe(row => row.ApprovedCorrection!.TimeEntryCorrectionId == correction.TimeEntryCorrectionId);
        foreach (var row in rebuilt.Page.Items)
        {
            row.EventLineage.Select(item => item.EventName).ShouldContain(nameof(TimeEntryApprovedCorrected));
        }

        // Model a reader returning older rows that still carry unfiltered comment values.
        var rawPage = projection.Project("tenant-1", events.Reverse(), checkpoint, fullQuery);
        var storedPage = rawPage with
        {
            Items = rawPage.Items.Select(row => row with
            {
                Comment = row.RowState == ApprovedTimeLedgerRowState.Current ? corrected : original,
                ApprovedCorrection = row.ApprovedCorrection! with
                {
                    PreviousValues = correction.PreviousValues with { Comment = original },
                    CorrectedValues = correction.CorrectedValues with { Comment = corrected }
                }
            }).ToArray()
        };
        var storedReader = Substitute.For<IApprovedTimeLedgerProjectionReader>();
        storedReader.QueryAsync(Arg.Any<TimesheetsRequestContext>(), fullQuery, token)
            .Returns(ValueTask.FromResult<ApprovedTimeLedgerReadModel?>(storedPage));
        var direct = new ApprovedTimeLedgerQueryService(guard, storedReader, new StaticHydrator(), options);
        var directRead = await direct.QueryAsync(Context(), fullQuery, token);
        JsonSerializer.Serialize(directRead.Page.ShouldNotBeNull().Items).ShouldBe(ledgerJson);

        ApprovedTimeExportService service = new(guard, ledger, new AcknowledgingExportTestRecorder(), options);
        ApprovedTimeExportPreviewReadModel preview = (await service.PreviewAsync(Context(), new() { LedgerQuery = query },
            correction.CorrectedAtUtc, token)).Preview.ShouldNotBeNull();
        preview.Readiness.ShouldBe(ApprovedTimeExportReadinessState.Ready);
        preview.CommentExportPolicy.ShouldBe(TimesheetsCommentPolicyDecision.Excluded);
        (preview.ReadinessDetail == "Comments are permitted for some values only.").ShouldBe(currentExported);
        ApprovedTimeExportResult generated = await service.GenerateAsync(Context(), new() { LedgerQuery = query },
            correction.CorrectedAtUtc, token);
        generated.WasGenerated.ShouldBeTrue();
        ApprovedTimeExportReadModel export = generated.Export.ShouldNotBeNull();
        export.Scope.ShouldBe(preview.Scope);
        export.Scope.LedgerCheckpoint.ShouldBe(rebuilt.Page.ExportSnapshotId.ShouldNotBeNull());
        export.Rows.Select(row => row.RowState).ShouldBe([ApprovedTimeLedgerRowState.Current, ApprovedTimeLedgerRowState.Superseded]);
        string json = JsonSerializer.Serialize(export.Rows);
        json.ShouldContain(correction.TimeEntryCorrectionId.Value);
        json.ShouldNotContain(original.Text);
        json.Contains(corrected.Text, StringComparison.Ordinal).ShouldBe(currentExported);
        string csv = export.CsvContent.ShouldNotBeNull();
        csv.ShouldNotContain(original.Text);
        csv.Contains(corrected.Text, StringComparison.Ordinal).ShouldBe(currentExported);
    }

    [Theory]
    [InlineData("tenant", true)]
    [InlineData("sensitivity", true)]
    [InlineData("legal-hold", true)]
    [InlineData("retention", true)]
    [InlineData("sensitivity", false)]
    public async Task MissingPolicyIsDisclosedOnlyAfterTenantAndReferenceAuthorization(string missing, bool canExport)
    {
        var options = FinanceExportTestPolicy.Configured with
        {
            CommentSensitivityPolicyConfigured = missing != "sensitivity",
            LegalHoldPolicyConfigured = missing != "legal-hold",
            TenantRetentionOverridePolicyConfigured = missing != "retention"
        };
        var context = missing == "tenant" ? Context() with { Tenant = new("another-authorized-tenant") } : Context();
        CancellationToken token = TestContext.Current.CancellationToken;
        var tenantAccess = Substitute.For<ITimesheetsTenantAccessValidator>();
        tenantAccess.ValidateAsync(context, TimesheetsOperation.Export, token)
            .Returns(ValueTask.FromResult(canExport ? TimesheetsTenantAccessResult.Authorized()
                : new TimesheetsTenantAccessResult(TimesheetsTenantAccessState.InsufficientRole, "Access denied.")));
        TimesheetsAccessGuard guard = new(tenantAccess, Substitute.For<IProjectReferenceValidator>(),
            Substitute.For<IWorkReferenceValidator>(), Substitute.For<IContributorPartyValidator>(),
            new TimesheetsEvidencePolicyEvaluator(options));
        var reader = Substitute.For<IApprovedTimeLedgerProjectionReader>();
        var recorder = Substitute.For<IApprovedTimeExportAuditRecorder>();
        var service = new ApprovedTimeExportService(guard, new(guard, reader, new StaticHydrator(), options), recorder, options);
        var preview = await service.PreviewAsync(context, new(), DateTimeOffset.UnixEpoch, token);
        preview.WasDisclosed.ShouldBe(canExport);
        if (canExport)
        {
            preview.Preview.ShouldNotBeNull().Readiness.ShouldBe(ApprovedTimeExportReadinessState.Blocked);
            preview.Preview.CommentExportPolicy.ShouldBe(TimesheetsCommentPolicyDecision.PolicyRequired);
        }
        else
        {
            preview.Preview.ShouldBeNull();
            preview.Authorization.DenialCategory.ShouldBe(TimesheetsDenialCategory.InsufficientRole);
        }
        (await service.GenerateAsync(context, new(), DateTimeOffset.UnixEpoch, token)).WasGenerated.ShouldBeFalse();
        reader.ReceivedCalls().ShouldBeEmpty();
        recorder.ReceivedCalls().ShouldBeEmpty();
    }

    private static TimeEntryProjectionEvent Event(string messageId, long sequenceNumber, object payload)
        => new(messageId, sequenceNumber, payload, "tenant-1");

    private static TimeEntryRecorded Recorded(
        string id,
        TimeEntryTargetReference target,
        int durationMinutes,
        PartyReference? contributor = null,
        ActivityTypeId? activityType = null,
        DateOnly? serviceDate = null,
        BillableState billableState = BillableState.Billable)
        => new(
            new TimeEntryId(id),
            target,
            contributor ?? Contributor(),
            activityType ?? ActivityType(),
            ActivityTypeScope.Tenant,
            serviceDate ?? new DateOnly(2026, 6, 19),
            durationMinutes,
            billableState,
            TimeEntryApprovalState.Draft,
            ContributorCategory.Employee,
            AiEffortMetrics.Unavailable);

    private static TimeEntrySubmitted Submitted(string id)
        => new(
            new TimeEntryId(id),
            new PartyReference("submitter-1"),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 6, 19, 12, 0, 0, TimeSpan.Zero),
            new TimeEntrySubmissionId("submission-" + id),
            TimeEntrySubmissionScope.SelectedEntries,
            TimeEntryApprovalState.Submitted);

    private static TimeEntryApproved Approved(string id, string decisionId)
        => new(
            new TimeEntryId(id),
            new PartyReference("approver-1"),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 6, 19, 13, 0, 0, TimeSpan.Zero),
            new TimeEntryApprovalDecisionId(decisionId),
            TimeEntryApprovalState.Approved,
            Authority(ApprovalAuthorityAction.EntryApproval),
            TimeEntryApprovalScope.IndividualEntry);

    private static TimeEntryApprovedCorrected ApprovedCorrected(
        string id,
        TimeEntryTargetReference target,
        int durationMinutes)
        => new(
            new TimeEntryId(id),
            new TimeEntryCorrectionId("approved-correction-1"),
            new TenantReference("tenant-1"),
            new PartyReference("operator-1"),
            new DateTimeOffset(2026, 6, 20, 9, 30, 0, TimeSpan.Zero),
            Values(target, 45),
            Values(target, durationMinutes),
            new TimeEntryCorrectionReason("Correct approved duration after audit review."),
            new TimeEntryApprovalDecisionId("decision-2"),
            TimeEntryApprovalScope.IndividualEntry,
            TimeEntryApprovalState.Approved,
            TimeEntryCorrectionState.Corrected);

    private static TimeEntryCorrectionValues Values(TimeEntryTargetReference target, int durationMinutes)
        => new(
            target,
            Contributor(),
            ActivityType(),
            new DateOnly(2026, 6, 19),
            durationMinutes,
            BillableState.Billable,
            ContributorCategory.Employee,
            AiEffortMetrics.Unavailable);

    private static ApprovedTimeExportRowReadModel GoldenRow(
        string id,
        TimeEntryTargetReference target,
        PartyReference contributor,
        ActivityTypeId activityTypeId,
        ActivityTypeScope activityTypeScope,
        DateOnly serviceDate,
        int durationMinutes,
        string approver,
        DateTimeOffset approvedAtUtc,
        ApprovedTimeLedgerRowState rowState,
        IReadOnlyList<TimeEntryEventLineageItem> lineage)
        => new(
            new TimeEntryId(id),
            contributor,
            target,
            serviceDate,
            durationMinutes,
            activityTypeId,
            activityTypeScope,
            BillableState.Billable,
            new(
                new TimeEntryId(id),
                new TimeEntryApprovalDecisionId("decision-" + id),
                new PartyReference(approver),
                new TenantReference("tenant-1"),
                approvedAtUtc,
                TimeEntryApprovalState.Approved,
                TimeEntryApprovalScope.IndividualEntry,
                Authority(ApprovalAuthorityAction.EntryApproval),
                null),
            rowState)
        {
            EventLineage = lineage
        };

    private static TimeEntryApprovedCorrectionEvidence GoldenApprovedCorrection(string id)
        => new(
            new TimeEntryId(id),
            new TimeEntryCorrectionId("approved-correction-1"),
            new PartyReference("operator-1"),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 10, 26, 8, 0, 0, TimeSpan.Zero),
            new TimeEntryCorrectionReason("Approved correction for reconciliation."),
            new TimeEntryApprovalDecisionId("decision-" + id),
            TimeEntryApprovalScope.IndividualEntry,
            Values(TimeEntryTargetReference.ForWork(Work()), 40),
            Values(TimeEntryTargetReference.ForWork(Work()), 45),
            TimeEntryApprovalState.Approved,
            TimeEntryCorrectionState.Corrected);

    private static TimeEntryCorrectionEvidence GoldenRejectedCorrection(string id)
        => new(
            new TimeEntryId(id),
            new TimeEntryCorrectionId("rejected-correction-1"),
            new PartyReference("operator-1"),
            new TenantReference("tenant-1"),
            new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero),
            new TimeEntryRejectionReason("Rejected entry corrected before approval."),
            new TimeEntryApprovalDecisionId("rejected-decision-" + id),
            Values(TimeEntryTargetReference.ForWork(Work()), 35),
            Values(TimeEntryTargetReference.ForWork(Work()), 45),
            TimeEntryCorrectionState.Corrected);

    private static TimeEntryComment AllowedExportComment(string text)
        => new(
            text,
            new(
                TimesheetsCommentPolicyDecision.Allowed,
                TimesheetsCommentPolicyDecision.Excluded,
                TimesheetsCommentPolicyDecision.Allowed,
                TimesheetsCommentPolicyDecision.Allowed,
                TimesheetsCommentPolicyDecision.Excluded,
                TimesheetsCommentRedactionRequirement.NotRequired,
                TimesheetsEvidenceRetentionCategory.CommentText));

    private static ApprovalAuthoritySourceAttribution Authority(ApprovalAuthorityAction action)
        => new(
            action,
            ApprovalAuthoritySource.ProjectApprover,
            ApprovalAuthorityDecisionState.Allowed,
            "timesheets.approval-authority.v1",
            "v1",
            ProjectionFreshnessMetadata.Fresh);

    private static TimesheetsRequestContext Context()
        => new(new TenantReference("tenant-1"), new PartyReference("operator-1"), "correlation-1");

    private static ProjectReference Project() => new("project-1");

    private static WorkReference Work() => new("work-1");

    private static PartyReference Contributor() => new("party-1");

    private static ActivityTypeId ActivityType() => new("activity-type-1");

    private sealed class StaticHydrator : ITimeEntryDisplayHydrator
    {
        public ValueTask<TimeEntryDisplayHydration> HydrateAsync(
            TimesheetsRequestContext context,
            TimeEntryEvidenceReadModel evidence,
            CancellationToken cancellationToken)
            => ValueTask.FromResult(new TimeEntryDisplayHydration(
                TimeEntryHydratedDisplayLabel.Fresh("Contributor"),
                TimeEntryHydratedDisplayLabel.Fresh("Target"),
                TimeEntryHydratedDisplayLabel.Fresh("Activity Type")));
    }

    private sealed class AllowAllAccessGuard : ITimesheetsAccessGuard
    {
        public ValueTask<TimesheetsAuthorizationDecision> AuthorizeAsync(
            TimesheetsAuthorizationRequest request,
            CancellationToken cancellationToken)
            => ValueTask.FromResult(TimesheetsAuthorizationDecision.Allowed());

        public async ValueTask<TimesheetsAuthorizationDecision> ExecuteIfAuthorizedAsync(
            TimesheetsAuthorizationRequest request,
            Func<CancellationToken, ValueTask> trustedWork,
            CancellationToken cancellationToken)
        {
            await trustedWork(cancellationToken).ConfigureAwait(false);
            return TimesheetsAuthorizationDecision.Allowed();
        }

        public ValueTask<TimesheetsUiActionPolicyOutcome> EvaluateUiActionAsync(
            TimesheetsAuthorizationRequest request,
            TimesheetsUiActionVisibility deniedVisibility,
            CancellationToken cancellationToken)
            => ValueTask.FromResult(TimesheetsUiActionPolicyOutcome.FromDecision(
                request.UiAction.GetValueOrDefault(),
                TimesheetsAuthorizationDecision.Allowed(),
                deniedVisibility));
    }
}
