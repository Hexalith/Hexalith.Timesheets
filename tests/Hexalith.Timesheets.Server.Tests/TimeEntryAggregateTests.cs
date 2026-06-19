using Hexalith.Timesheets.Contracts.Commands.TimeEntries;
using Hexalith.Timesheets.Contracts.Events.Rejections;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.ActivityTypes;
using Hexalith.Timesheets.Server.TimeEntries;

using Shouldly;

namespace Hexalith.Timesheets.Server.Tests;

public sealed class TimeEntryAggregateTests
{
    [Fact]
    public void Record_draft_time_entry_emits_stable_draft_event()
    {
        RecordTimeEntry command = ValidCommand() with { Comment = Comment() };

        TimesheetsDomainResult result = TimeEntry.Handle(command, null, ActivityTypeScope.Tenant);

        TimeEntryRecorded recorded = SingleSuccess<TimeEntryRecorded>(result);
        recorded.TimeEntryId.ShouldBe(command.TimeEntryId);
        recorded.Target.ShouldBe(command.Target);
        recorded.Contributor.ShouldBe(command.Contributor);
        recorded.ActivityTypeId.ShouldBe(command.ActivityTypeId);
        recorded.ActivityTypeScope.ShouldBe(ActivityTypeScope.Tenant);
        recorded.ServiceDate.ShouldBe(command.ServiceDate);
        recorded.DurationMinutes.ShouldBe(60);
        recorded.BillableState.ShouldBe(BillableState.Billable);
        recorded.ApprovalState.ShouldBe(TimeEntryApprovalState.Draft);
        recorded.ContributorCategory.ShouldBe(ContributorCategory.Employee);
        recorded.AiMetrics.ShouldBe(AiEffortMetrics.Unavailable);
        recorded.Comment.ShouldBe(command.Comment);
    }

    [Fact]
    public void Record_rejects_duplicate_time_entry_id_without_duplicate_event()
    {
        RecordTimeEntry command = ValidCommand();
        TimeEntryState state = new();
        state.Apply(new TimeEntryRecorded(
            command.TimeEntryId,
            command.Target,
            command.Contributor,
            command.ActivityTypeId,
            ActivityTypeScope.Tenant,
            command.ServiceDate,
            command.DurationMinutes,
            command.BillableState,
            TimeEntryApprovalState.Draft,
            command.ContributorCategory,
            command.AiMetrics));

        TimesheetsRejection rejection = Rejection(TimeEntry.Handle(command, state, ActivityTypeScope.Tenant));

        rejection.Code.ShouldBe(TimesheetsRejectionCode.ValidationFailed);
        rejection.FieldErrors.ShouldContain(static error => error.Field == "timeEntryId" && error.Code == "duplicate");
    }

    [Theory]
    [InlineData(0, "durationMinutes", "positive")]
    [InlineData(-5, "durationMinutes", "positive")]
    public void Record_rejects_non_positive_duration_with_field_error(
        int durationMinutes,
        string expectedField,
        string expectedCode)
    {
        TimesheetsRejection rejection = Rejection(TimeEntry.Handle(
            ValidCommand() with { DurationMinutes = durationMinutes },
            null,
            ActivityTypeScope.Tenant));

        rejection.FieldErrors.ShouldContain(error => error.Field == expectedField && error.Code == expectedCode);
    }

    [Fact]
    public void Record_rejects_missing_required_capture_fields()
    {
        RecordTimeEntry command = ValidCommand() with
        {
            Target = null!,
            Contributor = null!,
            ActivityTypeId = null!,
            BillableState = BillableState.Unknown,
            ContributorCategory = ContributorCategory.Unknown
        };

        TimesheetsRejection rejection = Rejection(TimeEntry.Handle(command, null, ActivityTypeScope.Unknown));

        rejection.FieldErrors.Select(static error => error.Field).ShouldContain("target");
        rejection.FieldErrors.Select(static error => error.Field).ShouldContain("contributor");
        rejection.FieldErrors.Select(static error => error.Field).ShouldContain("activityTypeId");
        rejection.FieldErrors.Select(static error => error.Field).ShouldContain("billableState");
        rejection.FieldErrors.Select(static error => error.Field).ShouldContain("contributorCategory");
        rejection.FieldErrors.Select(static error => error.Field).ShouldContain("activityTypeScope");
    }

    [Fact]
    public void Record_rejects_invalid_ai_metric_units()
    {
        RecordTimeEntry command = ValidCommand() with
        {
            AiMetrics = new(
                AiMetricAvailability.ProviderReported,
                -1,
                -2,
                -3,
                -4,
                -5,
                -6),
            Comment = null
        };

        TimesheetsRejection rejection = Rejection(TimeEntry.Handle(command, null, ActivityTypeScope.Tenant));

        rejection.FieldErrors.ShouldContain(static error => error.Field == "aiMetrics.wallClockDurationMilliseconds");
        rejection.FieldErrors.ShouldContain(static error => error.Field == "aiMetrics.modelRuntimeMilliseconds");
        rejection.FieldErrors.ShouldContain(static error => error.Field == "aiMetrics.billableEffortMinutes");
        rejection.FieldErrors.ShouldContain(static error => error.Field == "aiMetrics.providerInputTokenCount");
        rejection.FieldErrors.ShouldContain(static error => error.Field == "aiMetrics.providerOutputTokenCount");
        rejection.FieldErrors.ShouldContain(static error => error.Field == "aiMetrics.providerTotalTokenCount");
    }

    [Fact]
    public void Record_rejects_invalid_comment_policy_decisions()
    {
        RecordTimeEntry command = ValidCommand() with { Comment = InvalidComment() };

        TimesheetsRejection rejection = Rejection(TimeEntry.Handle(command, null, ActivityTypeScope.Tenant));

        rejection.Code.ShouldBe(TimesheetsRejectionCode.ValidationFailed);
        rejection.FieldErrors.ShouldContain(static error => error.Field == "comment.policy" && error.Code == "unknown");
    }

    private static RecordTimeEntry ValidCommand()
        => new(
            new TimeEntryId("time-entry-1"),
            TimeEntryTargetReference.ForProject(new ProjectReference("project-1")),
            new PartyReference("party-1"),
            new ActivityTypeId("activity-type-1"),
            new DateOnly(2026, 6, 19),
            60,
            BillableState.Billable,
            ContributorCategory.Employee,
            AiEffortMetrics.Unavailable);

    private static TimeEntryComment Comment()
        => new(
            "Worked on project delivery.",
            Hexalith.Timesheets.Contracts.Policies.TimeEntryCommentPolicy.SensitiveDefault);

    private static TimeEntryComment InvalidComment()
        => new(
            "Worked on project delivery.",
            Hexalith.Timesheets.Contracts.Policies.TimeEntryCommentPolicy.SensitiveDefault with
            {
                InternalDisplay = Hexalith.Timesheets.Contracts.Policies.TimesheetsCommentPolicyDecision.Unknown
            });

    private static TEvent SingleSuccess<TEvent>(TimesheetsDomainResult result)
    {
        result.IsSuccess.ShouldBeTrue();
        return result.Events.ShouldHaveSingleItem().ShouldBeOfType<TEvent>();
    }

    private static TimesheetsRejection Rejection(TimesheetsDomainResult result)
    {
        result.IsRejection.ShouldBeTrue();
        return result.Events.ShouldHaveSingleItem().ShouldBeOfType<TimesheetsRejection>();
    }
}
