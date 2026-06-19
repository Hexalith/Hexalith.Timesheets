using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.ValueObjects;

using CapabilityState = Hexalith.Timesheets.Contracts.ValueObjects.MagicLinkCapabilityState;

namespace Hexalith.Timesheets.Projections.MagicLinks;

public sealed class MagicLinkConfirmationCapabilityProjection
{
    public const string ProjectionName = "magic-link-confirmation-capabilities";

    public MagicLinkConfirmationCapabilityReadModel? Project(
        MagicLinkCapabilityId capabilityId,
        IEnumerable<MagicLinkProjectionEvent> events,
        TimesheetsProjectionCheckpoint checkpoint,
        DateTimeOffset observedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(capabilityId);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(checkpoint);

        MagicLinkConfirmationCapabilityReadModel? model = null;
        HashSet<string> appliedMessageIds = new(StringComparer.Ordinal);

        foreach (MagicLinkProjectionEvent projectionEvent in events
            .OrderBy(static projectionEvent => projectionEvent.SequenceNumber))
        {
            if (string.IsNullOrWhiteSpace(projectionEvent.MessageId)
                || !appliedMessageIds.Add(projectionEvent.MessageId))
            {
                continue;
            }

            if (projectionEvent.Payload is MagicLinkConfirmationCapabilityIssued issued
                && issued.CapabilityId == capabilityId)
            {
                model = Apply(issued, checkpoint, observedAtUtc);
            }
            else if (projectionEvent.Payload is MagicLinkConfirmationCapabilityRevoked revoked
                && revoked.CapabilityId == capabilityId
                && model is not null
                && model.State == CapabilityState.Issued)
            {
                model = Apply(revoked, model, checkpoint, observedAtUtc);
            }
            else if (projectionEvent.Payload is MagicLinkConfirmationCapabilityExpired expired
                && expired.CapabilityId == capabilityId
                && model is not null
                && model.State == CapabilityState.Issued)
            {
                model = Apply(expired, model, checkpoint, observedAtUtc);
            }
            else if (projectionEvent.Payload is MagicLinkConfirmationCapabilityUsed used
                && used.CapabilityId == capabilityId
                && model is not null
                && model.State == CapabilityState.Issued)
            {
                model = Apply(used, model, checkpoint, observedAtUtc);
            }
        }

        return model;
    }

    private static MagicLinkConfirmationCapabilityReadModel Apply(
        MagicLinkConfirmationCapabilityIssued issued,
        TimesheetsProjectionCheckpoint checkpoint,
        DateTimeOffset observedAtUtc)
    {
        MagicLinkExpiryState expiryState = ToExpiryState(issued.ExpiresAtUtc, observedAtUtc);

        return new(
            issued.CapabilityId,
            issued.Tenant,
            issued.Contributor,
            issued.Target,
            issued.ActivityTypeId,
            issued.TimeEntryId,
            issued.TargetKind,
            issued.AllowedAction,
            CapabilityState.Issued,
            expiryState,
            issued.ExpiresAtUtc,
            issued.Issuer,
            issued.IssuedAtUtc,
            ToFreshnessMetadata(checkpoint))
        {
            IssueMetadata = issued.Source,
            StateBadgeText = "Issued",
            ExpiryBadgeText = ToExpiryBadgeText(expiryState)
        };
    }

    private static MagicLinkConfirmationCapabilityReadModel Apply(
        MagicLinkConfirmationCapabilityRevoked revoked,
        MagicLinkConfirmationCapabilityReadModel current,
        TimesheetsProjectionCheckpoint checkpoint,
        DateTimeOffset observedAtUtc)
    {
        MagicLinkExpiryState expiryState = ToExpiryState(current.ExpiresAtUtc, observedAtUtc);

        return current with
        {
            State = CapabilityState.Revoked,
            ExpiryState = expiryState,
            RevokedBy = revoked.RevokedBy,
            RevokedAtUtc = revoked.RevokedAtUtc,
            RevocationMetadata = revoked.Source,
            ProjectionFreshness = ToFreshnessMetadata(checkpoint),
            StateBadgeText = "Revoked",
            ExpiryBadgeText = ToExpiryBadgeText(expiryState)
        };
    }

    private static MagicLinkConfirmationCapabilityReadModel Apply(
        MagicLinkConfirmationCapabilityExpired expired,
        MagicLinkConfirmationCapabilityReadModel current,
        TimesheetsProjectionCheckpoint checkpoint,
        DateTimeOffset observedAtUtc)
        => current with
        {
            State = CapabilityState.Expired,
            ExpiryState = MagicLinkExpiryState.Expired,
            ExpiredAtUtc = expired.ExpiredAtUtc,
            ExpiryMetadata = expired.Source,
            ProjectionFreshness = ToFreshnessMetadata(checkpoint),
            StateBadgeText = "Expired",
            ExpiryBadgeText = ToExpiryBadgeText(MagicLinkExpiryState.Expired)
        };

    private static MagicLinkConfirmationCapabilityReadModel Apply(
        MagicLinkConfirmationCapabilityUsed used,
        MagicLinkConfirmationCapabilityReadModel current,
        TimesheetsProjectionCheckpoint checkpoint,
        DateTimeOffset observedAtUtc)
    {
        MagicLinkExpiryState expiryState = ToExpiryState(current.ExpiresAtUtc, observedAtUtc);

        return current with
        {
            State = CapabilityState.Used,
            ExpiryState = expiryState,
            UsedAtUtc = used.UsedAtUtc,
            UseMetadata = used.Source,
            UseOutcomeCategory = used.OutcomeCategory,
            ProjectionFreshness = ToFreshnessMetadata(checkpoint),
            StateBadgeText = "Used",
            ExpiryBadgeText = ToExpiryBadgeText(expiryState)
        };
    }

    private static MagicLinkExpiryState ToExpiryState(DateTimeOffset expiresAtUtc, DateTimeOffset observedAtUtc)
    {
        if (observedAtUtc >= expiresAtUtc)
        {
            return MagicLinkExpiryState.Expired;
        }

        return expiresAtUtc - observedAtUtc <= TimeSpan.FromHours(24)
            ? MagicLinkExpiryState.ExpiringSoon
            : MagicLinkExpiryState.Active;
    }

    private static string ToExpiryBadgeText(MagicLinkExpiryState state)
        => state switch
        {
            MagicLinkExpiryState.Active => "Active",
            MagicLinkExpiryState.ExpiringSoon => "Expiring soon",
            MagicLinkExpiryState.Expired => "Expired",
            _ => "Unknown"
        };

    private static ProjectionFreshnessMetadata ToFreshnessMetadata(TimesheetsProjectionCheckpoint checkpoint)
        => checkpoint.Freshness switch
        {
            ProjectionFreshness.Fresh => new(
                ProjectionFreshnessState.Fresh,
                checkpoint.SequenceNumber.ToString(System.Globalization.CultureInfo.InvariantCulture),
                null,
                null),
            ProjectionFreshness.Rebuilding => ProjectionFreshnessMetadata.Rebuilding(),
            ProjectionFreshness.Stale => ProjectionFreshnessMetadata.Stale(
                checkpoint.SequenceNumber.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            ProjectionFreshness.Unavailable => ProjectionFreshnessMetadata.Unavailable(),
            _ => new(ProjectionFreshnessState.Unknown, null, null, "Projection freshness is unknown.")
        };
}
