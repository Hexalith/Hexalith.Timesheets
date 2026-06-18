namespace Hexalith.Timesheets.Contracts.References;

public sealed record PartyReference
{
    public PartyReference(string partyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partyId);
        PartyId = partyId;
    }

    public string PartyId { get; }
}
