using System.Text.Json;

using Hexalith.Timesheets.Contracts.Events.Exports;
using Hexalith.Timesheets.Contracts.Events.Rejections;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Policies;
using Hexalith.Timesheets.Contracts.Queries.Reporting;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.ApprovedTimeLedger;
using Hexalith.Timesheets.Server.Authorization;
using Hexalith.Timesheets.Server.Exports;
using Hexalith.Timesheets.Server.Policies;

using NSubstitute;

using Shouldly;

namespace Hexalith.Timesheets.Server.Tests;

public sealed partial class ApprovedTimeExportServiceTests
{
    [Fact]
    public void VersionOneReceiptCommandIdentityMatchesFixedVector()
    {
        var filters = new QueryApprovedTimeLedger { BillableState = BillableState.Billable };
        ApprovedTimeExported receipt = new(new("operator-1"), new("tenant-1"), filters,
            RequestedAtUtc(), RequestedAtUtc().AddSeconds(10), "trace-42", new(filters, 1, false, true),
            ApprovedTimeExportFormat.Csv, "approved-time-export.csv.v1", ProjectionFreshnessState.Fresh, 1, new string('a', 64))
        {
            RequestId = "example-request", OutputRowsHashSha256 = new string('b', 64)
        };
        ApprovedTimeExportReceipt.IsValid(receipt with { Tenant = new("sYsTeM") }, out _).ShouldBeFalse();
        ApprovedTimeExportReceipt.IsValid(receipt with { Tenant = new("Tenant-1") }, out string canonical).ShouldBeTrue();
        ApprovedTimeExportReceipt.Owner(canonical, receipt).ShouldBe("finance-export-0N206V26ZTKBPYQKGSTFD0ZYFW");
        ApprovedTimeExportReceipt.MessageId(receipt, "01ARZ3NDEKTSV4RRFFQ69G5FAV").ShouldBe("1RKQNGW1VMP7H1BKE9Z41KR6CZ");
    }

    [Fact]
    public async Task NullLedgerFiltersRespectCancellationAndAuthorizationBeforeReportingInvalidInput()
    {
        var token = TestContext.Current.CancellationToken;
        var context = Context();
        var command = Command() with { RequestId = "retry-key", LedgerQuery = null! };
        var reader = new TrackingLedgerReader(Page([]));
        var recorder = new TrackingAuditRecorder();
        var service = Service(new SequencedAccessGuard([TimesheetsAuthorizationDecision.Denied("No export role.")]),
            reader, new TrackingHydrator(), recorder);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Should.ThrowAsync<OperationCanceledException>(() => service.GenerateAsync(context, command, RequestedAtUtc(), cancelled.Token).AsTask());
        (await service.GenerateAsync(context, command, RequestedAtUtc(), token)).Export.ShouldBeNull();
        var blocked = await service.GenerateAsync(context, command, RequestedAtUtc(), token);
        AssertNoDelivery(blocked);
        blocked.Export!.Audit.RequestId.ShouldBe("retry-key");
        var preview = await service.PreviewAsync(context, new() { LedgerQuery = null! }, RequestedAtUtc(), token);
        preview.Preview!.Readiness.ShouldBe(ApprovedTimeExportReadinessState.Blocked);
        reader.Calls.ShouldBe(0);
        recorder.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task ExplicitRequestIdsAreBoundedAndControlFree()
    {
        var token = TestContext.Current.CancellationToken;
        var reader = new TrackingLedgerReader(Page([Row("entry", ProjectTarget())]));
        var recorder = new TrackingAuditRecorder();
        var service = Service(new SequencedAccessGuard([]), reader, new TrackingHydrator(), recorder);
        foreach (var id in new[] { "", "  ", "bad\nkey", new string('x', 257) })
        {
            var blocked = await service.GenerateAsync(Context(), Command() with { RequestId = id }, RequestedAtUtc(), token);
            AssertNoDelivery(blocked);
            blocked.Export!.Audit.RequestId.ShouldBeNull();
        }
        reader.Calls.ShouldBe(0);
        recorder.Events.ShouldBeEmpty();
        (await service.GenerateAsync(Context(), Command() with { RequestId = new string('x', 256) }, RequestedAtUtc(), token)).WasGenerated.ShouldBeTrue();
    }

    [Theory]
    [InlineData(true, true, true, false)]
    [InlineData(true, true, true, true)]
    [InlineData(true, false, false, true)]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, false, true)]
    [InlineData(true, true, false, true)]
    [InlineData(true, false, true, true)]
    public async Task PreviewAndSerializedOutputRequireTenantAndIndividualValuePermission(
        bool tenantAllows, bool currentAllows, bool historicalAllows, bool hostAllows)
    {
        TimeEntryComment Comment(string marker, bool allowed) => CommentWithExportDecision(marker, allowed
            ? TimesheetsCommentPolicyDecision.Allowed : TimesheetsCommentPolicyDecision.Excluded);
        ApprovedTimeLedgerRowReadModel row = RowWithCorrections(Comment, currentAllows, historicalAllows);
        TrackingAuditRecorder recorder = new();
        ApprovedTimeExportService service = Service(new SequencedAccessGuard([]), new TrackingLedgerReader(Page([row])),
            new TrackingHydrator(), recorder, FinanceExportTestPolicy.Configured with
            {
                ExportCommentsAllowed = hostAllows,
                ExportCommentsAllowedByTenant = new Dictionary<string, bool> { ["tenant-1"] = tenantAllows }
            });
        CancellationToken token = TestContext.Current.CancellationToken;
        ApprovedTimeExportPreviewReadModel preview = (await service.PreviewAsync(Context(),
            new() { LedgerQuery = Command().LedgerQuery }, RequestedAtUtc(), token)).Preview.ShouldNotBeNull();
        recorder.Events.ShouldBeEmpty();
        preview.Readiness.ShouldBe(ApprovedTimeExportReadinessState.Ready);
        bool currentVisible = hostAllows && tenantAllows && currentAllows;
        bool historyVisible = hostAllows && tenantAllows && historicalAllows;
        preview.CommentExportPolicy.ShouldBe(currentVisible && historyVisible
            ? TimesheetsCommentPolicyDecision.Allowed : TimesheetsCommentPolicyDecision.Excluded);
        (preview.ReadinessDetail == "Comments are permitted for some values only.").ShouldBe(currentVisible != historyVisible);
        JsonSerializer.Serialize(preview).ShouldNotContain("-marker");

        ApprovedTimeExportResult result = await service.GenerateAsync(Context(), Command(), RequestedAtUtc(), token);
        result.WasGenerated.ShouldBeTrue();
        ApprovedTimeExportReadModel export = result.Export.ShouldNotBeNull();
        export.CsvContent.ShouldNotBeNull().Contains("current-marker", StringComparison.Ordinal).ShouldBe(currentVisible);
        string serialized = JsonSerializer.Serialize(export.Rows);
        foreach (string marker in new[] { "current-marker", "approved-new-marker", "rejected-new-marker" })
        {
            serialized.Contains(marker, StringComparison.Ordinal).ShouldBe(currentVisible);
        }
        foreach (string marker in new[] { "approved-old-marker", "rejected-old-marker" })
        {
            serialized.Contains(marker, StringComparison.Ordinal).ShouldBe(historyVisible);
        }
        serialized.ShouldContain("approved-correction-id");
        serialized.ShouldContain("rejected-correction-id");
        serialized.ShouldContain("TimeEntryRecorded");
        JsonSerializer.Serialize(recorder.Events.ShouldHaveSingleItem()).ShouldNotContain("-marker");
    }

    [Fact]
    public async Task PreviewSummarizesCorrectionPermissionsWithoutReadingExportLineage()
    {
        var lineage = Substitute.For<IReadOnlyList<TimeEntryEventLineageItem>>();
        var row = RowWithCorrections((marker, allowed) => CommentWithExportDecision(marker,
            allowed ? TimesheetsCommentPolicyDecision.Allowed : TimesheetsCommentPolicyDecision.Excluded),
            currentAllowed: false, historicalAllowed: true) with { EventLineage = lineage };
        var service = Service(new SequencedAccessGuard([]), new TrackingLedgerReader(Page([row])), new TrackingHydrator());
        var preview = (await service.PreviewAsync(Context(), new(), RequestedAtUtc(),
            TestContext.Current.CancellationToken)).Preview.ShouldNotBeNull();
        preview.CommentExportPolicy.ShouldBe(TimesheetsCommentPolicyDecision.Excluded);
        preview.ReadinessDetail.ShouldBe("Comments are permitted for some values only.");
        lineage.ReceivedCalls().ShouldBeEmpty();
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    [InlineData(true, 0)]
    [InlineData(true, 99)]
    [InlineData(false, 2)]
    public async Task DirectQueryRechecksEveryStoredCommentBeforeDisclosure(bool hostAllows, int version)
    {
        TimeEntryComment Comment(string text, bool allowed) => new(text, TimeEntryCommentPolicy.SensitiveDefault with
        {
            ProjectionInclusion = allowed ? TimesheetsCommentPolicyDecision.Excluded : TimesheetsCommentPolicyDecision.Allowed,
            ServerProjectionInclusion = allowed ? TimesheetsCommentPolicyDecision.Allowed : TimesheetsCommentPolicyDecision.Excluded,
            ServerPublicationVersion = version
        });
        ApprovedTimeLedgerRowReadModel row = RowWithCorrections(Comment, true, false);
        ApprovedTimeLedgerQueryService service = new(new SequencedAccessGuard([]), new TrackingLedgerReader(Page([row])),
            new TrackingHydrator(), FinanceExportTestPolicy.Configured with { ProjectionCommentsAllowed = hostAllows });
        ApprovedTimeLedgerQueryResult result = await service.QueryAsync(Context(), Command().LedgerQuery,
            TestContext.Current.CancellationToken);
        string json = JsonSerializer.Serialize(result.Page.ShouldNotBeNull().Items.ShouldHaveSingleItem());
        foreach (string marker in new[] { "current-marker", "approved-new-marker", "rejected-new-marker" })
        {
            json.Contains(marker, StringComparison.Ordinal).ShouldBe(hostAllows && version is 1 or 2);
        }
        json.ShouldNotContain("approved-old-marker");
        json.ShouldNotContain("rejected-old-marker");
        json.ShouldContain("approved-correction-id");
        json.ShouldContain("rejected-correction-id");
    }

    [Theory]
    [InlineData("missing-tenant")]
    [InlineData("wrong-case")]
    [InlineData("missing-retention")]
    [InlineData("missing-sensitivity")]
    public async Task AuthorizedMissingPolicyBlocksBeforeProtectedReads(string fault)
    {
        TimesheetsEvidencePolicyOptions options = fault switch
        {
            "missing-tenant" => FinanceExportTestPolicy.Configured with { ExportCommentsAllowedByTenant = new Dictionary<string, bool>() },
            "wrong-case" => FinanceExportTestPolicy.Configured with
            {
                ExportCommentsAllowedByTenant = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase) { ["TENANT-1"] = true }
            },
            "missing-retention" => FinanceExportTestPolicy.Configured with { LegalHoldPolicyConfigured = false },
            _ => FinanceExportTestPolicy.Configured with { CommentSensitivityPolicyConfigured = false }
        };
        TrackingLedgerReader reader = new(Page([Row("entry", ProjectTarget())]));
        TrackingAuditRecorder recorder = new();
        ApprovedTimeExportService service = Service(new SequencedAccessGuard([]), reader, new TrackingHydrator(), recorder, options);
        CancellationToken token = TestContext.Current.CancellationToken;
        ApprovedTimeExportPreviewReadModel preview = (await service.PreviewAsync(Context(), new(), RequestedAtUtc(), token)).Preview.ShouldNotBeNull();
        preview.Readiness.ShouldBe(ApprovedTimeExportReadinessState.Blocked);
        preview.CommentExportPolicy.ShouldBe(TimesheetsCommentPolicyDecision.PolicyRequired);
        AssertNoDelivery(await service.GenerateAsync(Context(), Command(), RequestedAtUtc(), token));
        reader.Calls.ShouldBe(0);
        recorder.Events.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("default")]
    [InlineData("in-memory")]
    [InlineData("rejected")]
    [InlineData("no-op")]
    [InlineData("wrong-event")]
    [InlineData("extra-event")]
    [InlineData("mismatch")]
    [InlineData("mismatched-checkpoint")]
    [InlineData("non-utc-receipt")]
    [InlineData("uncertain")]
    [InlineData("timeout")]
    public async Task UnverifiedAcknowledgementWithholdsAllDelivery(string fault)
    {
        IApprovedTimeExportAuditRecorder recorder = Substitute.For<IApprovedTimeExportAuditRecorder>();
        recorder.RecordAcceptedExportAsync(Arg.Any<ApprovedTimeExported>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                ApprovedTimeExported evidence = call.Arg<ApprovedTimeExported>();
                return ValueTask.FromResult(fault switch
                {
                    "rejected" => TimesheetsDomainResult.Rejection([new(TimesheetsRejectionCode.PolicyDenied, "Rejected", [])]),
                    "wrong-event" => TimesheetsDomainResult.Success([evidence.OutputScope]),
                    "extra-event" => TimesheetsDomainResult.Success([evidence, evidence]),
                    "mismatch" => TimesheetsDomainResult.Success([evidence with { OutputContentHashSha256 = "wrong" }]),
                    "mismatched-checkpoint" => TimesheetsDomainResult.Success([evidence with
                    {
                        OutputScope = evidence.OutputScope with { LedgerCheckpoint = "101" }
                    }]),
                    "non-utc-receipt" => TimesheetsDomainResult.Success([evidence with
                    {
                        GeneratedAtUtc = evidence.GeneratedAtUtc.ToOffset(TimeSpan.FromHours(1))
                    }]),
                    "uncertain" => throw new TimeoutException(),
                    "timeout" => throw new OperationCanceledException(),
                    _ => TimesheetsDomainResult.NoOp()
                });
            });
        SequencedAccessGuard guard = new([]);
        ApprovedTimeExportService service = new(guard, new ApprovedTimeLedgerQueryService(guard,
            new TrackingLedgerReader(Page([Row("entry", ProjectTarget())])), new TrackingHydrator()),
            fault switch { "default" => null, "in-memory" => new DomainEventApprovedTimeExportAuditRecorder(), _ => recorder },
            FinanceExportTestPolicy.Configured);
        AssertNoDelivery(await service.GenerateAsync(Context(), Command(), RequestedAtUtc(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExportNormalizesRequestInstantsBeforeRecordingAndComparingReceipts()
    {
        DateTimeOffset local = RequestedAtUtc().ToOffset(TimeSpan.FromHours(4));
        TrackingAuditRecorder recorder = new();
        ApprovedTimeExportService service = Service(new SequencedAccessGuard([]),
            new TrackingLedgerReader(Page([Row("entry", ProjectTarget())])), new TrackingHydrator(), recorder);
        CancellationToken token = TestContext.Current.CancellationToken;
        var preview = (await service.PreviewAsync(Context(), new(), local, token)).Preview.ShouldNotBeNull();
        preview.Audit.RequestedAtUtc.Offset.ShouldBe(TimeSpan.Zero);
        ApprovedTimeExportResult result = await service.GenerateAsync(Context(), Command(), local, token);
        result.WasGenerated.ShouldBeTrue();
        var receipt = recorder.Events.ShouldHaveSingleItem();
        receipt.RequestedAtUtc.Offset.ShouldBe(TimeSpan.Zero);
        receipt.GeneratedAtUtc.Offset.ShouldBe(TimeSpan.Zero);
        receipt.RequestedAtUtc.ShouldBe(local);
    }

    [Theory]
    [InlineData("approval-tenant")]
    [InlineData("approval-entry")]
    [InlineData("approval-state")]
    [InlineData("approved-correction-tenant")]
    [InlineData("approved-correction-entry")]
    [InlineData("correction-tenant")]
    [InlineData("correction-entry")]
    public async Task ContradictoryOwnerEvidenceIsOpaqueBeforeHydration(string contradiction)
    {
        var row = RowWithCorrections((marker, _) => AllowedExportComment(marker), true, true);
        row = contradiction switch
        {
            "approval-tenant" => row with { ApprovalDecision = row.ApprovalDecision with { Tenant = new("other") } },
            "approval-entry" => row with { ApprovalDecision = row.ApprovalDecision with { TimeEntryId = new("other") } },
            "approval-state" => row with { ApprovalDecision = row.ApprovalDecision with { ApprovalState = TimeEntryApprovalState.Rejected } },
            "approved-correction-tenant" => row with { ApprovedCorrection = row.ApprovedCorrection! with { Tenant = new("other") } },
            "approved-correction-entry" => row with { ApprovedCorrection = row.ApprovedCorrection! with { TimeEntryId = new("other") } },
            "correction-tenant" => row with { Correction = row.Correction! with { Tenant = new("other") } },
            _ => row with { Correction = row.Correction! with { TimeEntryId = new("other") } }
        };
        TrackingHydrator hydrator = new();
        var ledger = new ApprovedTimeLedgerQueryService(new SequencedAccessGuard([]), new TrackingLedgerReader(Page([row])), hydrator);
        var result = await ledger.QueryAsync(Context(), Command().LedgerQuery, TestContext.Current.CancellationToken);
        result.Authorization.IsAuthorized.ShouldBeFalse();
        result.Page.ShouldBeNull();
        hydrator.Calls.ShouldBe(0);
    }

    [Theory]
    [InlineData(TimesheetsDenialCategory.None)]
    [InlineData(TimesheetsDenialCategory.InsufficientRole)]
    [InlineData(TimesheetsDenialCategory.CrossTenantTarget)]
    public async Task LedgerChecksDistinctHistoricalTargetAndContributorScopes(TimesheetsDenialCategory denial)
    {
        var row = RowWithCorrections((marker, _) => AllowedExportComment(marker), true, true);
        var approved = row.ApprovedCorrection!;
        var rejected = row.Correction!;
        row = row with
        {
            ApprovedCorrection = approved with
            {
                PreviousValues = approved.PreviousValues with { Target = WorkTarget(), Contributor = new("historical-party") }
            },
            Correction = rejected with
            {
                CorrectedValues = rejected.CorrectedValues with { Target = WorkTarget(), Contributor = new("historical-party") },
                PreviousValues = rejected.PreviousValues with { Contributor = new("other-party") }
            }
        };
        List<TimesheetsAuthorizationRequest> requests = [];
        ITimesheetsAccessGuard guard = Substitute.For<ITimesheetsAccessGuard>();
        guard.AuthorizeAsync(Arg.Any<TimesheetsAuthorizationRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            var request = call.Arg<TimesheetsAuthorizationRequest>();
            requests.Add(request);
            return ValueTask.FromResult(request.Work?.WorkId == "work-1" && request.Contributor?.PartyId == "historical-party"
                && denial != TimesheetsDenialCategory.None
                ? TimesheetsAuthorizationDecision.Denied(denial, "History withheld.") : TimesheetsAuthorizationDecision.Allowed());
        });
        TrackingHydrator hydrator = new();
        var ledger = new ApprovedTimeLedgerQueryService(guard, new TrackingLedgerReader(Page([row])), hydrator);
        var result = await ledger.QueryAsync(Context(), Command().LedgerQuery, TestContext.Current.CancellationToken);
        switch (denial)
        {
            case TimesheetsDenialCategory.None:
                result.Page.ShouldNotBeNull().Items.ShouldHaveSingleItem();
                requests.Count.ShouldBe(4); // Tenant, current row and two distinct historical scopes.
                hydrator.Calls.ShouldBe(1);
                break;
            case TimesheetsDenialCategory.InsufficientRole:
                result.Page.ShouldNotBeNull().Items.ShouldBeEmpty();
                hydrator.Calls.ShouldBe(0);
                break;
            default:
                result.Page.ShouldBeNull();
                hydrator.Calls.ShouldBe(0);
                break;
        }
    }

    [Fact]
    public async Task CallerCancellationDuringAcknowledgementPropagates()
    {
        using CancellationTokenSource cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        IApprovedTimeExportAuditRecorder recorder = Substitute.For<IApprovedTimeExportAuditRecorder>();
        recorder.RecordAcceptedExportAsync(Arg.Any<ApprovedTimeExported>(), cancellation.Token)
            .Returns(call =>
            {
                cancellation.Cancel();
                return ValueTask.FromResult(TimesheetsDomainResult.Success([call.Arg<ApprovedTimeExported>()]));
            });
        ApprovedTimeExportService service = Service(new SequencedAccessGuard([]),
            new TrackingLedgerReader(Page([Row("entry", ProjectTarget())])), new TrackingHydrator(), recorder);
        await Should.ThrowAsync<OperationCanceledException>(() =>
            service.GenerateAsync(Context(), Command(), RequestedAtUtc(), cancellation.Token).AsTask());
    }

    private static void AssertNoDelivery(ApprovedTimeExportResult result)
    {
        result.WasGenerated.ShouldBeFalse();
        ApprovedTimeExportReadModel blocked = result.Export.ShouldNotBeNull();
        blocked.Readiness.ShouldBe(ApprovedTimeExportReadinessState.Blocked);
        blocked.Rows.ShouldBeEmpty();
        blocked.Scope.RowCount.ShouldBe(0);
        blocked.CsvContent.ShouldBeNull();
        blocked.Audit.OutputContentHashSha256.ShouldBeNull();
        blocked.Audit.RowCount.ShouldBe(0);
    }

    private static ApprovedTimeLedgerRowReadModel RowWithCorrections(
        Func<string, bool, TimeEntryComment> comment, bool currentAllowed, bool historicalAllowed)
    {
        ApprovedTimeLedgerRowReadModel row = Row("entry", ProjectTarget());
        TimeEntryCorrectionValues values = new(row.Target, row.Contributor, row.ActivityTypeId, row.ServiceDate,
            row.DurationMinutes, row.BillableState, row.ContributorCategory, null);
        return row with
        {
            Comment = comment("current-marker", currentAllowed),
            ApprovedCorrection = new(row.TimeEntryId, new("approved-correction-id"), row.Contributor, new("tenant-1"),
                RequestedAtUtc(), new("duration"), new("approval"), TimeEntryApprovalScope.IndividualEntry,
                values with { Comment = comment("approved-old-marker", historicalAllowed) },
                values with { Comment = comment("approved-new-marker", currentAllowed) },
                TimeEntryApprovalState.Approved, TimeEntryCorrectionState.Corrected),
            Correction = new(row.TimeEntryId, new("rejected-correction-id"), row.Contributor, new("tenant-1"),
                RequestedAtUtc(), new("duration"), new("rejection"),
                values with { Comment = comment("rejected-old-marker", historicalAllowed) },
                values with { Comment = comment("rejected-new-marker", currentAllowed) },
                TimeEntryCorrectionState.Corrected)
        };
    }
}
