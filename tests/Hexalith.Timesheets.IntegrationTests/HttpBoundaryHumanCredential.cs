using System.Security.Claims;
using System.Text;

using Microsoft.AspNetCore.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Hexalith.Timesheets.IntegrationTests;

/// <summary>Supplies real signed credentials for the host's existing human bearer policy.</summary>
internal static class HttpBoundaryHumanCredential
{
    /// <summary>Configures the fixture's issuer without replacing any authentication or authorization service.</summary>
    internal static void Configure(IWebHostBuilder builder)
    {
        builder.UseSetting("Authentication:JwtBearer:Issuer", "timesheets-test-issuer");
        builder.UseSetting("Authentication:JwtBearer:Audience", "hexalith-eventstore");
        builder.UseSetting("Authentication:JwtBearer:SigningKey", Convert.ToBase64String(Encoding.UTF8.GetBytes(new string('k', 32))));
        builder.UseSetting("Authentication:JwtBearer:AllowedAlgorithms:0", SecurityAlgorithms.HmacSha256);
        builder.UseSetting("Authentication:JwtBearer:RequireHttpsMetadata", "false");
    }

    /// <summary>Creates a signed human token, or a signed workload-kind token for refusal checks.</summary>
    internal static string Create(string actor, bool workload = false)
        => new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "timesheets-test-issuer",
            Audience = "hexalith-eventstore",
            Subject = new ClaimsIdentity(workload
                ? [new Claim(ClaimTypes.NameIdentifier, actor), new Claim("eventstore:workload", "eventstore")]
                : [new Claim(ClaimTypes.NameIdentifier, actor)]),
            NotBefore = DateTime.UtcNow.AddMinutes(-1),
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Convert.ToBase64String(Encoding.UTF8.GetBytes(new string('k', 32))))), SecurityAlgorithms.HmacSha256)
        });
}
