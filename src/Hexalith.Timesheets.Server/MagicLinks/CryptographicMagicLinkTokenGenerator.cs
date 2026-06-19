using System.Security.Cryptography;
using System.Text;

using Hexalith.Timesheets.Contracts.ValueObjects;

namespace Hexalith.Timesheets.Server.MagicLinks;

public sealed class CryptographicMagicLinkTokenGenerator : IMagicLinkTokenGenerator
{
    public MagicLinkTokenMaterial Generate()
    {
        byte[] bytes = RandomNumberGenerator.GetBytes(32);
        string token = ToBase64Url(bytes);
        return new(token, DeriveHash(token));
    }

    public MagicLinkTokenHash DeriveHash(string oneTimeToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(oneTimeToken);

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(oneTimeToken));
        return new MagicLinkTokenHash(ToBase64Url(hash));
    }

    private static string ToBase64Url(byte[] bytes)
        => Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
