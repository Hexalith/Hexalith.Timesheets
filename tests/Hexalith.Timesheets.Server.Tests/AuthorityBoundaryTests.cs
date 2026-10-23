using Shouldly;

namespace Hexalith.Timesheets.Server.Tests;

public sealed class AuthorityBoundaryTests
{
    private static readonly string[] ForbiddenAuthorityTokens =
    [
        "TimesheetsRequestContext",
        "ClaimsPrincipal",
        "AuthorizationHandlerContext",
        "authorizationContext",
        "tenantMembership",
        "TenantMembership",
        "PartyDisplayName",
        "ProjectDisplayName",
        "WorkDisplayName",
        "accessToken",
        "bearerToken",
        "jwt",
        "serverControlledTenant",
        "serverControlledUser"
    ];

    [Fact]
    public void PublicContractsAndClientDoNotExposeServerAuthorityContext()
    {
        string[] publicSourceFiles =
        [
            .. SourceFiles(TestRepositoryRoot.PathTo("src", "Hexalith.Timesheets.Contracts")),
            .. SourceFiles(TestRepositoryRoot.PathTo("src", "Hexalith.Timesheets.Client"))
        ];

        publicSourceFiles.ShouldNotBeEmpty();

        foreach (string sourceFile in publicSourceFiles)
        {
            string source = File.ReadAllText(sourceFile);

            foreach (string forbidden in ForbiddenAuthorityTokens)
            {
                source.ShouldNotContain(forbidden, Case.Insensitive, sourceFile);
            }
        }
    }

    [Fact]
    public void ServerAuthorityContextIsDefinedOnlyInTheServerKernel()
    {
        string[] matchingFiles = Directory
            .GetFiles(TestRepositoryRoot.PathTo("src"), "*.cs", SearchOption.AllDirectories)
            .Where(static file => !file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .Where(static file => File.ReadAllText(file).Contains("TimesheetsRequestContext", StringComparison.Ordinal))
            .ToArray();

        matchingFiles.ShouldNotBeEmpty();

        // The server authority context is owned by the Server kernel. Story 1.10 introduces the first
        // concrete sibling reference-validation adapter (Hexalith.Timesheets.Works), which legitimately
        // consumes the Server-owned IWorkReferenceValidator seam and therefore takes a
        // TimesheetsRequestContext parameter. Public surfaces (Contracts/Client) are still forbidden the
        // type by PublicContractsAndClientDoNotExposeServerAuthorityContext. The host's
        // Development-only isolated fixture also implements the server-owned tenant-validation seam;
        // permit that exact adapter without allowing other host or presentation code.
        matchingFiles.ShouldAllBe(file =>
            file.Contains(Path.Combine("src", "Hexalith.Timesheets.Server"), StringComparison.Ordinal)
            || file.Contains(Path.Combine("src", "Hexalith.Timesheets.Works"), StringComparison.Ordinal)
            || file == TestRepositoryRoot.PathTo("src", "Hexalith.Timesheets", "Runtime", "LiveDaprFixtureAccessGuard.cs"));
    }

    private static string[] SourceFiles(string root)
    {
        return Directory
            .GetFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(static file => !file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .ToArray();
    }
}
