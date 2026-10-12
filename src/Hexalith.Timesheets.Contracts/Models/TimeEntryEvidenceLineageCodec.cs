using System.Buffers.Binary;

using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Contracts.Models;

/// <summary>Losslessly packs the bounded event and payload lineage of a Time Entry owner.</summary>
public static class TimeEntryEvidenceLineageCodec
{
    private const int MaxEventCount = 10_000;
    private const int EventItemSize = 10;
    private const int DigestSize = 32;

    private static readonly string[] s_eventNames =
    [
        "TimeEntryRecorded", "TimeEntrySubmitted", "TimeEntryContributorConfirmed",
        "TimeEntryAdjustedThroughMagicLink", "TimeEntryApproved", "TimeEntryRejected",
        "TimeEntryCorrected", "TimeEntryApprovedCorrected"
    ];

    /// <summary>Packs recognized lineage items into fixed-width binary records.</summary>
    public static byte[] PackEvents(IReadOnlyList<TimeEntryEventLineageItem> lineage)
    {
        ArgumentNullException.ThrowIfNull(lineage);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(lineage.Count, MaxEventCount);
        byte[] packed = new byte[checked(lineage.Count * EventItemSize)];
        for (int index = 0; index < lineage.Count; index++)
        {
            TimeEntryEventLineageItem item = lineage[index];
            int eventCode = Array.IndexOf(s_eventNames, item.EventName);
            if (eventCode < 0 || item.Ordinal <= 0
                || item.SourceAuthority != TimeEntryEvidenceSourceAuthority.TimesheetsDomainEvents)
            {
                throw new ArgumentException("Time Entry lineage contains an unsupported item.", nameof(lineage));
            }

            int offset = index * EventItemSize;
            packed[offset] = checked((byte)(eventCode + 1));
            BinaryPrimitives.WriteInt64LittleEndian(packed.AsSpan(offset + 1, 8), item.Ordinal);
            packed[offset + 9] = (byte)item.SourceAuthority;
        }

        return packed;
    }

    /// <summary>Restores recognized lineage items from their fixed-width representation.</summary>
    public static IReadOnlyList<TimeEntryEventLineageItem> UnpackEvents(byte[] packed)
    {
        ArgumentNullException.ThrowIfNull(packed);
        if (packed.Length % EventItemSize != 0 || packed.Length > MaxEventCount * EventItemSize)
        {
            throw new InvalidOperationException("Stored Time Entry event lineage has an invalid length.");
        }

        var items = new TimeEntryEventLineageItem[packed.Length / EventItemSize];
        for (int index = 0; index < items.Length; index++)
        {
            int offset = index * EventItemSize;
            int eventCode = packed[offset] - 1;
            long ordinal = BinaryPrimitives.ReadInt64LittleEndian(packed.AsSpan(offset + 1, 8));
            if ((uint)eventCode >= s_eventNames.Length || ordinal <= 0
                || packed[offset + 9] != (byte)TimeEntryEvidenceSourceAuthority.TimesheetsDomainEvents)
            {
                throw new InvalidOperationException("Stored Time Entry event lineage contains an invalid item.");
            }

            items[index] = new TimeEntryEventLineageItem(s_eventNames[eventCode], ordinal,
                TimeEntryEvidenceSourceAuthority.TimesheetsDomainEvents);
        }

        return items;
    }

    /// <summary>Packs complete SHA-256 payload digests as raw bytes.</summary>
    public static byte[] PackPayloads(IReadOnlyList<string> digests)
    {
        ArgumentNullException.ThrowIfNull(digests);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(digests.Count, MaxEventCount);
        byte[] packed = new byte[checked(digests.Count * DigestSize)];
        for (int index = 0; index < digests.Count; index++)
        {
            if (digests[index]?.Length != DigestSize * 2)
            {
                throw new ArgumentException("Time Entry payload lineage contains an invalid digest.", nameof(digests));
            }

            try
            {
                Convert.FromHexString(digests[index]).CopyTo(packed, index * DigestSize);
            }
            catch (FormatException exception)
            {
                throw new ArgumentException("Time Entry payload lineage contains an invalid digest.",
                    nameof(digests), exception);
            }
        }

        return packed;
    }

    /// <summary>Restores the complete ordered SHA-256 payload digest lineage.</summary>
    public static IReadOnlyList<string> UnpackPayloads(byte[] packed)
    {
        ArgumentNullException.ThrowIfNull(packed);
        if (packed.Length % DigestSize != 0 || packed.Length > MaxEventCount * DigestSize)
        {
            throw new InvalidOperationException("Stored Time Entry payload lineage has an invalid length.");
        }

        var digests = new string[packed.Length / DigestSize];
        for (int index = 0; index < digests.Length; index++)
        {
            digests[index] = Convert.ToHexString(packed.AsSpan(index * DigestSize, DigestSize));
        }

        return digests;
    }
}
