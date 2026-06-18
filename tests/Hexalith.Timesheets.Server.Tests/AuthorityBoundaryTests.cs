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
    public void Public_contracts_and_client_do_not_expose_server_authority_context()
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
    public void Server_authority_context_is_defined_only_in_the_server_kernel()
    {
        string[] matchingFiles = Directory
            .GetFiles(TestRepositoryRoot.PathTo("src"), "*.cs", SearchOption.AllDirectories)
            .Where(static file => !file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .Where(static file => File.ReadAllText(file).Contains("TimesheetsRequestContext", StringComparison.Ordinal))
            .ToArray();

        matchingFiles.ShouldNotBeEmpty();
        matchingFiles.ShouldAllBe(file => file.Contains(
            Path.Combine("src", "Hexalith.Timesheets.Server"),
            StringComparison.Ordinal));
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
