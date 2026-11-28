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

        // The Server kernel owns the context. Works and these HTTP host files consume it for
        // server-side reference validation, authorized ledger reads, and authenticated ingress.
        string[] hostConsumers =
        [
            TestRepositoryRoot.PathTo("src", "Hexalith.Timesheets", "Runtime", "LiveDaprFixtureAccessGuard.cs"),
            TestRepositoryRoot.PathTo("src", "Hexalith.Timesheets", "Runtime", "ProjectWorkflow", "GatewayProjectWorkflowReferences.cs"),
            TestRepositoryRoot.PathTo("src", "Hexalith.Timesheets", "Runtime", "ProjectWorkflow", "ProjectWorkflowLedgerReader.cs"),
            TestRepositoryRoot.PathTo("src", "Hexalith.Timesheets", "Endpoints", "ProjectWorkflowEndpoints.cs")
        ];
        matchingFiles.ShouldAllBe(file =>
            file.Contains(Path.Combine("src", "Hexalith.Timesheets.Server"), StringComparison.Ordinal)
            || file.Contains(Path.Combine("src", "Hexalith.Timesheets.Works"), StringComparison.Ordinal)
            || hostConsumers.Contains(file, StringComparer.Ordinal));
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
