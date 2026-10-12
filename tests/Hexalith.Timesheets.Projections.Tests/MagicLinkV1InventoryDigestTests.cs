using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hexalith.Timesheets.Contracts.References;
using Hexalith.Timesheets.Contracts.ValueObjects;
using Hexalith.Timesheets.Projections.MagicLinks;
using Hexalith.Timesheets.Server.MagicLinks;

using Shouldly;

namespace Hexalith.Timesheets.Projections.Tests;

/// <summary>Literal byte vectors calculated independently with Python hashlib and big-endian struct.pack.</summary>
public sealed class MagicLinkV1InventoryDigestTests
{
    [Fact]
    public void VersionOneReceiptFingerprintMatchesIndependentLiteralVector()
    {
        const string encoded = """{"ReceiptId":"receipt","SourceCutRevision":"r\u00E9v","InstallationCoverageReceiptId":"coverage","QuiescenceReceiptId":"quiet","ExpectedContributionCount":2,"ExpectedContributionDigest":"04EE1E02323065CCDB7D69C6AFF01FF7FD6109147B949255D356D59C783DD9E2"}""";
        var receipt = new MagicLinkV1InventoryReceipt("receipt", "rév", "coverage", "quiet", 2,
            "04EE1E02323065CCDB7D69C6AFF01FF7FD6109147B949255D356D59C783DD9E2");
        JsonSerializer.Serialize(receipt).ShouldBe(encoded);
        Encoding.UTF8.GetByteCount(encoded).ShouldBe(253);
        MagicLinkV1InventoryDigest.ReceiptFingerprint(receipt).ShouldBe("267D878BB8FB69EA9E52A1772BF304A935D86EA0B38BD583C0ABF70902CE7E28");
    }

    [Fact]
    public void VersionOneContributionEncodingMatchesIndependentLiteralVectors()
    {
        const string plain = """{"TokenHash":"hash","Candidate":{"Tenant":{"TenantId":"tenant"},"CapabilityId":{"Value":"owner"}}}""";
        const string escaped = """{"TokenHash":"hash","Candidate":{"Tenant":{"TenantId":"t\u00E9\u0022\u003C\\\n"},"CapabilityId":{"Value":"o\uD83D\uDE00\t"}}}""";
        MagicLinkTokenHashCapabilityIndexDirectoryEntry first = new("hash", new(new TenantReference("tenant"), new MagicLinkCapabilityId("owner")));
        MagicLinkTokenHashCapabilityIndexDirectoryEntry second = new("hash", new(new TenantReference("té\"<\\\n"), new MagicLinkCapabilityId("o😀\t")));
        JsonSerializer.Serialize(first).ShouldBe(plain);
        JsonSerializer.Serialize(second).ShouldBe(escaped);
        Encoding.UTF8.GetByteCount(plain).ShouldBe(98);
        Encoding.UTF8.GetByteCount(escaped).ShouldBe(125);
        using IncrementalHash digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        MagicLinkV1InventoryDigest.Append(digest, first);
        Convert.ToHexString(digest.GetHashAndReset()).ShouldBe("64B91D7A15C35CD0198D99BD1DB2B645472C5B0C235416DFE61BB5A8F8BDE640");
        MagicLinkV1InventoryDigest.Append(digest, second);
        Convert.ToHexString(digest.GetHashAndReset()).ShouldBe("B96FE4194C1EBCC0CA986B4AEBDD585B35913ADD9601F199C4E9A1BEEEACBB7F");
        MagicLinkV1InventoryDigest.Append(digest, first);
        MagicLinkV1InventoryDigest.Append(digest, second);
        Convert.ToHexString(digest.GetHashAndReset()).ShouldBe("04EE1E02323065CCDB7D69C6AFF01FF7FD6109147B949255D356D59C783DD9E2");
        Convert.ToHexString(digest.GetHashAndReset()).ShouldBe("E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855");
    }
}
