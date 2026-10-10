using Hexalith.Timesheets.Contracts.Events.MagicLinks;
using Hexalith.Timesheets.Contracts.Models;
using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.MagicLinks;

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

        var seen = new Dictionary<(bool OwnerStream, long Sequence), object>();
        var ordered = new List<MagicLinkProjectionEvent>();
        bool issuedInCapabilityStream = false;
        bool terminalInCapabilityStream = false;
        foreach (MagicLinkProjectionEvent item in events.OrderBy(static value => value.SequenceNumber))
        {
            // Stored terminal wrappers belong to the Time Entry stream; issuance and legacy
            // terminal events belong to the capability stream. Their sequence spaces differ.
            bool ownerStream = item.Payload is StoredMagicLinkUsed or StoredMagicLinkRevoked or StoredMagicLinkExpired;
            object payload = Unwrap(item.Payload);
            if (!ownerStream && payload is MagicLinkConfirmationCapabilityIssued issued
                && issued.CapabilityId == capabilityId)
            {
                if (terminalInCapabilityStream)
                {
                    throw new InvalidOperationException("Capability issuance follows a terminal transition.");
                }

                issuedInCapabilityStream = true;
            }
            else if (!ownerStream && IsTerminalFor(payload, capabilityId))
            {
                if (!issuedInCapabilityStream)
                {
                    throw new InvalidOperationException("Capability transition precedes issuance.");
                }

                terminalInCapabilityStream = true;
            }

            var key = (ownerStream, item.SequenceNumber);
            if (seen.TryGetValue(key, out object? existing))
            {
                if (existing.GetType() != payload.GetType() || !Equals(existing, payload))
                {
                    throw new InvalidOperationException("Capability delivery has contradictory evidence.");
                }

                continue;
            }

            seen.Add(key, payload);
            ordered.Add(item with { Payload = payload });
        }

        // The issue event belongs to the capability stream. New terminal events belong to a
        // different Time Entry stream, where sequence numbers are not comparable with issuance.
        MagicLinkConfirmationCapabilityIssued[] issuances = ordered
            .Select(static projectionEvent => projectionEvent.Payload)
            .OfType<MagicLinkConfirmationCapabilityIssued>()
            .Where(issued => issued.CapabilityId == capabilityId)
            .ToArray();
        if (issuances.Length == 0)
        {
            return null;
        }

        MagicLinkConfirmationCapabilityIssued issuance = issuances[0];
        if (issuances.Length > 1)
        {
            throw new InvalidOperationException("Capability issuance history is contradictory.");
        }

        MagicLinkConfirmationCapabilityReadModel model = Apply(issuance, checkpoint, observedAtUtc);

        foreach (MagicLinkProjectionEvent projectionEvent in ordered)
        {
            if (projectionEvent.Payload is MagicLinkConfirmationCapabilityRevoked conflictingRevoke
                && conflictingRevoke.CapabilityId == capabilityId
                && (conflictingRevoke.Tenant != issuance.Tenant
                    || (conflictingRevoke.TimeEntryId is not null
                        && conflictingRevoke.TimeEntryId != issuance.TimeEntryId)))
            {
                throw new InvalidOperationException("Capability revocation does not match issuance.");
            }

            if (projectionEvent.Payload is MagicLinkConfirmationCapabilityExpired conflictingExpire
                && conflictingExpire.CapabilityId == capabilityId
                && (conflictingExpire.Tenant != issuance.Tenant
                    || (conflictingExpire.TimeEntryId is not null
                        && conflictingExpire.TimeEntryId != issuance.TimeEntryId)))
            {
                throw new InvalidOperationException("Capability expiry does not match issuance.");
            }

            if (projectionEvent.Payload is MagicLinkConfirmationCapabilityUsed conflictingUse
                && conflictingUse.CapabilityId == capabilityId
                && (conflictingUse.Tenant != issuance.Tenant
                    || conflictingUse.TimeEntryId != issuance.TimeEntryId))
            {
                throw new InvalidOperationException("Capability use does not match issuance.");
            }

            if (IsTerminalFor(projectionEvent.Payload, capabilityId)
                && model.State != CapabilityState.Issued)
            {
                throw new InvalidOperationException("Capability has multiple terminal transitions.");
            }

            if (projectionEvent.Payload is MagicLinkConfirmationCapabilityRevoked revoked
                && revoked.CapabilityId == capabilityId
                && (revoked.TimeEntryId is null || revoked.TimeEntryId == model.TimeEntryId)
                && model.State == CapabilityState.Issued)
            {
                model = Apply(revoked, model, checkpoint, observedAtUtc);
            }
            else if (projectionEvent.Payload is MagicLinkConfirmationCapabilityExpired expired
                && expired.CapabilityId == capabilityId
                && (expired.TimeEntryId is null || expired.TimeEntryId == model.TimeEntryId)
                && model.State == CapabilityState.Issued)
            {
                model = Apply(expired, model, checkpoint, observedAtUtc);
            }
            else if (projectionEvent.Payload is MagicLinkConfirmationCapabilityUsed used
                && used.CapabilityId == capabilityId
                && used.TimeEntryId == model.TimeEntryId
                && model.State == CapabilityState.Issued)
            {
                model = Apply(used, model, checkpoint, observedAtUtc);
            }
        }

        return model;
    }

    private static object Unwrap(object payload) => payload switch
    {
        StoredMagicLinkIssued item => item.Event,
        StoredMagicLinkUsed item => item.Event,
        StoredMagicLinkRevoked item => item.Event,
        StoredMagicLinkExpired item => item.Event,
        _ => payload
    } ?? throw new InvalidOperationException("Stored capability evidence is missing.");

    private static bool IsTerminalFor(object payload, MagicLinkCapabilityId capabilityId)
        => payload switch
        {
            MagicLinkConfirmationCapabilityRevoked item => item.CapabilityId == capabilityId,
            MagicLinkConfirmationCapabilityExpired item => item.CapabilityId == capabilityId,
            MagicLinkConfirmationCapabilityUsed item => item.CapabilityId == capabilityId,
            _ => false
        };

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
