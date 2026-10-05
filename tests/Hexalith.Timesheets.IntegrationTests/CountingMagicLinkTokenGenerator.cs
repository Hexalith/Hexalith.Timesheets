using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Server.MagicLinks;

namespace Hexalith.Timesheets.IntegrationTests;

/// <summary>Counts issuance material generation while retaining the production cryptographic behavior.</summary>
internal sealed class CountingMagicLinkTokenGenerator : IMagicLinkTokenGenerator
{
    private readonly CryptographicMagicLinkTokenGenerator _inner = new();

    /// <summary>Gets the number of issuance material generations.</summary>
    internal int GenerationCount { get; private set; }

    /// <inheritdoc/>
    public MagicLinkTokenMaterial Generate()
    {
        GenerationCount++;
        return _inner.Generate();
    }

    /// <inheritdoc/>
    public MagicLinkTokenHash DeriveHash(string oneTimeToken) => _inner.DeriveHash(oneTimeToken);
}
