using Shouldly;

namespace Hexalith.Timesheets.ArchitectureTests.FitnessTests;

public sealed class DiagnosticsPrivacyTests
{
    private static readonly string[] SensitiveLoggingTerms =
    [
        "command body",
        "commandBody",
        "event payload",
        "eventPayload",
        "comment",
        "personal data",
        "token",
        "secret",
        "magic-link",
        "PartyReference.Value",
        "ProjectReference.Value",
        "WorkReference.Value"
    ];

    [Fact]
    public void Source_logging_does_not_include_sensitive_payload_or_identifier_material()
    {
        string[] sourceFiles = Directory.GetFiles(RepositoryRoot.PathTo("src"), "*.cs", SearchOption.AllDirectories);
        sourceFiles.ShouldNotBeEmpty();

        foreach (string sourceFile in sourceFiles)
        {
            string[] loggingLines = File.ReadAllLines(sourceFile)
                .Where(static line => line.Contains("Log", StringComparison.Ordinal)
                    || line.Contains("logger", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            foreach (string loggingLine in loggingLines)
            {
                foreach (string sensitiveTerm in SensitiveLoggingTerms)
                {
                    loggingLine.ShouldNotContain(sensitiveTerm, Case.Insensitive, sourceFile);
                }
            }
        }
    }

    [Fact]
    public void Host_metadata_endpoint_exposes_correlation_safe_module_metadata_only()
    {
        string hostProgram = File.ReadAllText(RepositoryRoot.PathTo("src", "Hexalith.Timesheets", "Program.cs"));

        hostProgram.ShouldContain("MapGet");
        hostProgram.ShouldContain("/metadata/timesheets");
        hostProgram.ShouldContain("Hexalith.Timesheets");
        hostProgram.ShouldContain("timesheets");
        hostProgram.ShouldContain("Hexalith.Timesheets.Server");
        hostProgram.ShouldNotContain("TenantReference");
        hostProgram.ShouldNotContain("PartyReference");
        hostProgram.ShouldNotContain("ProjectReference");
        hostProgram.ShouldNotContain("WorkReference");
        hostProgram.ShouldNotContain("token", Case.Insensitive);
        hostProgram.ShouldNotContain("secret", Case.Insensitive);
    }
}
