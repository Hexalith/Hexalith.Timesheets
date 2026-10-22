using System.Text.Json;

using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Events.TimeEntries;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.MagicLinks;
using Hexalith.Timesheets.Server.TimeEntries;

using Shouldly;

namespace Hexalith.Timesheets.Server.Tests;

public sealed class TimeEntryStateTests
{
    [Fact]
    public void IllegalLifecycleEventsLeaveEveryFieldUnchanged()
    {
        (object[] History, Type[] Allowed)[] cases =
        [
            ([], [typeof(TimeEntryRecorded)]),
            ([Recorded()], [typeof(TimeEntrySubmitted), typeof(TimeEntryContributorConfirmed), typeof(TimeEntryAdjustedThroughMagicLink)]),
            ([Recorded(), Submitted()], [typeof(TimeEntryApproved), typeof(TimeEntryRejected), typeof(TimeEntryContributorConfirmed)]),
            ([Recorded(), Submitted(), Rejected()], [typeof(TimeEntryCorrected), typeof(TimeEntryContributorConfirmed)]),
            ([Recorded(), Submitted(), Rejected(), Corrected()], [typeof(TimeEntrySubmitted), typeof(TimeEntryContributorConfirmed)]),
            ([Recorded(), Submitted(), Approved()], [typeof(TimeEntryApprovedCorrected), typeof(TimeEntryContributorConfirmed)]),
            ([Recorded(), Submitted(), Approved(), ApprovedCorrected()], [typeof(TimeEntryContributorConfirmed)]),
            ([Recorded(), Submitted(), Rejected(), Corrected(), Submitted(), Rejected()], [typeof(TimeEntryContributorConfirmed)])
        ];
        foreach ((object[] history, Type[] allowed) in cases)
        {
            foreach (object next in new object[] { Recorded(), Submitted(), Confirmed(), Adjusted(), Approved(), Rejected(), Corrected(), ApprovedCorrected() })
            {
                if (allowed.Contains(next.GetType())) continue;
                var state = new TimeEntryState();
                foreach (object item in history) Apply(state, item);
                string before = JsonSerializer.Serialize(state);

                Should.Throw<InvalidOperationException>(() => Apply(state, next));

                JsonSerializer.Serialize(state).ShouldBe(before);
            }
        }
    }

    [Fact]
    public void WrongDestinationStatesLeaveEveryFieldUnchanged()
    {
        (object[] Prefix, object Invalid)[] cases =
        [
            ([], Recorded() with { ApprovalState = TimeEntryApprovalState.Approved }),
            ([], Recorded() with { ApprovalState = (TimeEntryApprovalState)99 }),
            ([Recorded()], Submitted() with { ApprovalState = TimeEntryApprovalState.Draft }),
            ([Recorded(), Submitted()], Approved() with { ApprovalState = TimeEntryApprovalState.Rejected }),
            ([Recorded(), Submitted()], Rejected() with { ApprovalState = TimeEntryApprovalState.Approved }),
            ([Recorded(), Submitted(), Rejected()], Corrected() with { ApprovalState = TimeEntryApprovalState.Approved }),
            ([Recorded(), Submitted(), Rejected()], Corrected() with { CorrectionState = TimeEntryCorrectionState.None }),
            ([Recorded(), Submitted(), Approved()], ApprovedCorrected() with { ApprovalState = TimeEntryApprovalState.Draft }),
            ([Recorded(), Submitted(), Approved()], ApprovedCorrected() with { CorrectionState = TimeEntryCorrectionState.None })
        ];
        foreach ((object[] prefix, object invalid) in cases)
        {
            var state = new TimeEntryState();
            foreach (object item in prefix) Apply(state, item);
            string before = JsonSerializer.Serialize(state);

            Should.Throw<InvalidOperationException>(() => Apply(state, invalid));

            JsonSerializer.Serialize(state).ShouldBe(before);
        }
    }

    [Fact]
    public void CorrectionAttributionMustMatchPrecedingDecisionBeforeMutation()
    {
        (object[] Prefix, object Invalid)[] cases =
        [
            ([Recorded(), Submitted(), Approved()], ApprovedCorrected() with { SourceApprovalDecisionId = new("other-decision") }),
            ([Recorded(), Submitted(), Approved()], ApprovedCorrected() with { SourceApprovalScope = TimeEntryApprovalScope.TimesheetPeriod }),
            ([Recorded(), Submitted(), Rejected()], Corrected() with { RejectionDecisionId = new("other-decision") }),
            ([Recorded(), Submitted(), Rejected()], Corrected() with { RejectionReason = new("Other reason") })
        ];
        foreach ((object[] prefix, object invalid) in cases)
        {
            var state = new TimeEntryState();
            foreach (object item in prefix) Apply(state, item);
            string before = JsonSerializer.Serialize(state);
            Should.Throw<InvalidOperationException>(() => Apply(state, invalid));
            JsonSerializer.Serialize(state).ShouldBe(before);
        }
    }

    [Fact]
    public void ContradictoryExplicitScopesLeaveEveryFieldUnchanged()
    {
        TimeEntryAdjustedThroughMagicLink adjustment = Adjusted();
        (object[] Prefix, object Invalid)[] cases =
        [
            ([Recorded()], adjustment with { PreviousValues = Values(ActivityTypeScope.Project) }),
            ([Recorded()], adjustment with { AdjustedValues = Values(ActivityTypeScope.Project) }),
            ([Recorded(), Submitted(), Rejected()], Corrected() with { PreviousValues = Values(ActivityTypeScope.Project) }),
            ([Recorded(), Submitted(), Approved()], ApprovedCorrected() with { PreviousValues = Values(ActivityTypeScope.Project) })
        ];
        foreach ((object[] prefix, object invalid) in cases)
        {
            var state = new TimeEntryState();
            foreach (object item in prefix) Apply(state, item);
            string before = JsonSerializer.Serialize(state);
            Should.Throw<InvalidOperationException>(() => Apply(state, invalid));
            JsonSerializer.Serialize(state).ShouldBe(before);
        }
    }

    [Fact]
    public void ValidWrappedAndLegacyHistoriesPreserveScopeAndLineage()
    {
        var state = new TimeEntryState();
        state.Apply(new MagicLinkCommitRejected());
        state.Apply(new StoredTimeEntryRecorded(Recorded()));
        state.Apply(new StoredTimeEntryAdjusted(Adjusted() with
        {
            PreviousValues = Values(null), AdjustedValues = Values(null) with { DurationMinutes = 75 }
        }));
        state.Apply(new StoredTimeEntrySubmitted(Submitted()));
        state.Apply(Rejected());
        state.Apply(Corrected() with { PreviousValues = Values(null) with { DurationMinutes = 75 }, CorrectedValues = Values(null) });
        state.ActivityTypeScope.ShouldBe(ActivityTypeScope.Tenant);
        state.RejectionDecisionId.ShouldBe(Rejected().TimeEntryApprovalDecisionId);
        state.Apply(new StoredTimeEntrySubmitted(Submitted() with { TimeEntrySubmissionId = new("resubmission-1") }));
        state.Apply(Approved());
        state.Apply(new StoredTimeEntryConfirmed(Confirmed()));
        state.ApprovalState.ShouldBe(TimeEntryApprovalState.Approved);
        state.IsLockedFromDirectEdit.ShouldBeTrue();
        state.ConfirmedAtUtc.ShouldBe(Confirmed().ConfirmedAtUtc);

        var approved = new TimeEntryState();
        approved.Apply(Recorded() with { ActivityTypeScope = ActivityTypeScope.Project });
        approved.Apply(Submitted());
        approved.Apply(Approved());
        approved.Apply(ApprovedCorrected() with { PreviousValues = Values(null), CorrectedValues = Values(null) });
        approved.ActivityTypeScope.ShouldBe(ActivityTypeScope.Project);
        approved.SourceApprovalDecisionId.ShouldBe(Approved().TimeEntryApprovalDecisionId);
        approved.LockState.ShouldBe(TimeEntryLockState.LockedFromDirectEdit);
    }

    [Fact]
    public void CapabilityTerminalBookkeepingDoesNotRequireRecordingAndSurvivesRefusal()
    {
        var state = new TimeEntryState();
        var terminal = new MagicLinkConfirmationCapabilityUsed(new("capability-1"), new("tenant-1"),
            new("contributor-1"), new("entry-1"), DateTimeOffset.UnixEpoch, new("magic-link", "use-1"));
        state.Apply(new StoredMagicLinkUsed(terminal));
        state.HasTerminalMagicLinkCapability(terminal.CapabilityId).ShouldBeTrue();
        state.Apply(Recorded());
        string before = JsonSerializer.Serialize(state);
        Should.Throw<InvalidOperationException>(() => state.Apply(new StoredTimeEntryRecorded(Recorded())));
        JsonSerializer.Serialize(state).ShouldBe(before);
        state.Apply(terminal);
        state.HasAmbiguousMagicLinkTerminalHistory.ShouldBeTrue();
        before = JsonSerializer.Serialize(state);
        state.Apply(new MagicLinkCommitRejected());
        JsonSerializer.Serialize(state).ShouldBe(before);
    }

    private static void Apply(TimeEntryState state, object item)
    {
        switch (item)
        {
            case TimeEntryRecorded value: state.Apply(value); break;
            case TimeEntrySubmitted value: state.Apply(value); break;
            case TimeEntryContributorConfirmed value: state.Apply(value); break;
            case TimeEntryAdjustedThroughMagicLink value: state.Apply(value); break;
            case TimeEntryApproved value: state.Apply(value); break;
            case TimeEntryRejected value: state.Apply(value); break;
            case TimeEntryCorrected value: state.Apply(value); break;
            case TimeEntryApprovedCorrected value: state.Apply(value); break;
            default: throw new InvalidOperationException();
        }
    }

    private static TimeEntryRecorded Recorded() => new(new("entry-1"), Values().Target, new("contributor-1"),
        new("activity-1"), ActivityTypeScope.Tenant, new(2026, 6, 19), 60, BillableState.Billable,
        TimeEntryApprovalState.Draft, ContributorCategory.ExternalContributor, null);

    private static TimeEntrySubmitted Submitted() => new(new("entry-1"), new("operator-1"), new("tenant-1"),
        DateTimeOffset.UnixEpoch, new("submission-1"), TimeEntrySubmissionScope.SelectedEntries, TimeEntryApprovalState.Submitted);

    private static TimeEntryContributorConfirmed Confirmed() => new(new("entry-1"), new("contributor-1"),
        new("tenant-1"), DateTimeOffset.UnixEpoch, new("magic-link", "confirm-1"));

    private static TimeEntryAdjustedThroughMagicLink Adjusted() => new(new("entry-1"), new("tenant-1"),
        new("contributor-1"), DateTimeOffset.UnixEpoch, ActivityTypeScope.Tenant, Values(), Values(), new("magic-link", "adjust-1"));

    private static TimeEntryApproved Approved() => new(new("entry-1"), new("operator-1"), new("tenant-1"),
        DateTimeOffset.UnixEpoch, new("decision-1"), TimeEntryApprovalState.Approved, Authority(), TimeEntryApprovalScope.IndividualEntry);

    private static TimeEntryRejected Rejected() => new(new("entry-1"), new("operator-1"), new("tenant-1"),
        DateTimeOffset.UnixEpoch, new("decision-1"), TimeEntryApprovalState.Rejected,
        Authority() with { Action = ApprovalAuthorityAction.EntryRejection }, TimeEntryApprovalScope.IndividualEntry, new("Needs correction"));

    private static TimeEntryCorrected Corrected() => new(new("entry-1"), new("correction-1"), new("tenant-1"),
        new("operator-1"), DateTimeOffset.UnixEpoch, Values(), Values(), new("Needs correction"), new("decision-1"),
        TimeEntryApprovalState.Draft, TimeEntryCorrectionState.Corrected);

    private static TimeEntryApprovedCorrected ApprovedCorrected() => new(new("entry-1"), new("correction-1"), new("tenant-1"),
        new("operator-1"), DateTimeOffset.UnixEpoch, Values(), Values(), new("Correction"), new("decision-1"),
        TimeEntryApprovalScope.IndividualEntry, TimeEntryApprovalState.Approved, TimeEntryCorrectionState.Corrected);

    private static TimeEntryCorrectionValues Values(ActivityTypeScope? scope = ActivityTypeScope.Tenant) => new(
        TimeEntryTargetReference.ForProject(new ProjectReference("project-1")), new("contributor-1"), new("activity-1"),
        new(2026, 6, 19), 60, BillableState.Billable, ContributorCategory.ExternalContributor, null) { ActivityTypeScope = scope };

    private static ApprovalAuthoritySourceAttribution Authority() => new(ApprovalAuthorityAction.EntryApproval,
        ApprovalAuthoritySource.ProjectApprover, ApprovalAuthorityDecisionState.Allowed, "policy", "v1", ProjectionFreshnessMetadata.Fresh);
}
