using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Hexalith.Timesheets.IntegrationTests;

/// <summary>Checks the bounded listener configuration wiring, not arbitrary program semantics.</summary>
internal static class InternalSurfaceConfigurationSyntax
{
    /// <summary>Inspects decoded tokens and constant strings in the two checked entry points.</summary>
    /// <param name="appHost">The AppHost source.</param>
    /// <param name="host">The HTTP host source.</param>
    /// <returns>Fixed descriptions of unsupported or unsafe wiring.</returns>
    internal static IReadOnlyList<string> Validate(string appHost, string host)
    {
        List<string> failures = [];
        Check(appHost, isAppHost: true, failures);
        Check(host, isAppHost: false, failures);
        return failures;
    }

    private static void Check(string source, bool isAppHost, List<string> failures)
    {
        SyntaxTree tree = CSharpSyntaxTree.ParseText(source);
        SyntaxNode root = tree.GetRoot();
        if (tree.GetDiagnostics().Any(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            || root.DescendantTrivia().Any(static trivia => trivia.IsDirective))
        {
            failures.Add("The checked wiring must parse without conditional directives.");
            return;
        }

        SemanticModel model = CSharpCompilation.Create("ListenerConfigurationFitness", [tree],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)])
            .GetSemanticModel(tree);
        string? ConstantString(ExpressionSyntax expression) => model.GetConstantValue(expression) is { HasValue: true, Value: string value }
            ? value : null;
        static string MethodName(InvocationExpressionSyntax invocation) => invocation.Expression is MemberAccessExpressionSyntax member
            ? member.Name.Identifier.ValueText : string.Empty;
        static bool Sensitive(string value) => Regex.IsMatch(value, @"Timesheets(?:__|:)InternalSurface(?:__|:)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        List<InvocationExpressionSyntax> timesheetsCalls = [];
        if (isAppHost)
        {
            VariableDeclaratorSyntax[] ports = root.DescendantNodes().OfType<VariableDeclaratorSyntax>()
                .Where(static variable => variable.Identifier.ValueText == "InternalPort").ToArray();
            if (ports.Length != 1 || model.GetDeclaredSymbol(ports[0]) is not ILocalSymbol
                { IsConst: true, Type.SpecialType: SpecialType.System_Int32, ConstantValue: 8081 })
            {
                failures.Add("The internal listener port must be the immutable integer 8081.");
            }
            VariableDeclaratorSyntax[] resources = root.DescendantNodes().OfType<VariableDeclaratorSyntax>()
                .Where(static variable => variable.Identifier.ValueText == "timesheets").ToArray();
            ExpressionSyntax? chain = resources.Length == 1 ? resources[0].Initializer?.Value : null;
            bool projectFound = false;
            while (chain is InvocationExpressionSyntax call && call.Expression is MemberAccessExpressionSyntax member)
            {
                timesheetsCalls.Add(call);
                string method = member.Name.Identifier.ValueText;
                if (method == "AddProject")
                {
                    projectFound = true;
                    break;
                }
                if (method is not ("WithHttpEndpoint" or "WithEnvironment" or "WithArgs"))
                {
                    failures.Add("The checked Timesheets configuration chain contains an unsupported call.");
                }
                chain = member.Expression;
            }
            if (!projectFound) failures.Add("The checked Timesheets project chain is missing.");
        }

        if (root.DescendantTokens().Any(static token => token.IsKind(SyntaxKind.IdentifierToken)
            && string.Equals(token.ValueText, "AllowOnAnyPort", StringComparison.OrdinalIgnoreCase)))
        {
            failures.Add("The checked wiring must not reference the listener bypass property.");
        }

        ExpressionSyntax? canonicalKey = null;
        int exports = 0;
        foreach (InvocationExpressionSyntax invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            string method = MethodName(invocation);
            if (isAppHost && invocation.Expression is MemberAccessExpressionSyntax
                {
                    Expression: MemberAccessExpressionSyntax
                    {
                        Name.Identifier.ValueText: "Annotations",
                        Expression: MemberAccessExpressionSyntax
                        {
                            Name.Identifier.ValueText: "Resource",
                            Expression: IdentifierNameSyntax { Identifier.ValueText: "timesheets" },
                        },
                    },
                } && method != "OfType" && !IsFixtureGatewayWaitRemoval(invocation))
            {
                failures.Add("Direct Timesheets annotation mutation is unsupported by the bounded wiring check.");
            }
            if (isAppHost && method == "WithArgs")
            {
                foreach (ArgumentSyntax argument in invocation.ArgumentList.Arguments)
                {
                    if (ConstantString(argument.Expression) is not string value || Sensitive(value))
                    {
                        failures.Add("AppHost arguments must be evaluable constants without internal listener overrides.");
                    }
                }
            }
            if (isAppHost && method == "WithEnvironment")
            {
                SeparatedSyntaxList<ArgumentSyntax> arguments = invocation.ArgumentList.Arguments;
                string? key = arguments.Count == 2 ? ConstantString(arguments[0].Expression) : null;
                if (key is null)
                {
                    failures.Add("AppHost environment keys must be evaluable constant strings.");
                }
                else if (Sensitive(key))
                {
                    exports++;
                    canonicalKey = arguments[0].Expression;
                    if (key != "Timesheets__InternalSurface__Port"
                        || !timesheetsCalls.Any(call => call.Span == invocation.Span)
                        || !SyntaxFactory.AreEquivalent(arguments[1].Expression,
                            SyntaxFactory.ParseExpression("InternalPort.ToString(System.Globalization.CultureInfo.InvariantCulture)")))
                    {
                        failures.Add("The internal listener export must use the fixed internal port.");
                    }
                }
            }
        }

        // Inspect maximal constant expressions once: Unicode escapes and concatenation
        // are decoded by Roslyn. Comments never become syntax expressions.
        foreach (ExpressionSyntax expression in root.DescendantNodes().OfType<ExpressionSyntax>())
        {
            if (ConstantString(expression) is not string value || !Sensitive(value)
                || expression.Parent is ExpressionSyntax parent && ConstantString(parent) is not null)
            {
                continue;
            }
            if (canonicalKey is null || expression.Span != canonicalKey.Span)
            {
                failures.Add("The checked wiring contains another internal listener configuration string.");
            }
        }

        if (isAppHost)
        {
            if (exports != 1) failures.Add("Exactly one internal listener environment export is required.");
        }
        else
        {
            InvocationExpressionSyntax[] bindings = root.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Where(invocation => invocation.Expression is MemberAccessExpressionSyntax
                {
                    Name: GenericNameSyntax generic,
                } && generic.Identifier.ValueText is "Configure" or "PostConfigure" or "ConfigureAll" or "PostConfigureAll"
                    && generic.TypeArgumentList.Arguments.Any(static type => type.GetLastToken().ValueText == "InternalSurfaceOptions"))
                .ToArray();
            ExpressionSyntax expected = SyntaxFactory.ParseExpression(
                "builder.Services.Configure<InternalSurfaceOptions>(builder.Configuration.GetSection(InternalSurfaceOptions.SectionName))");
            if (bindings.Length != 1 || !SyntaxFactory.AreEquivalent(bindings[0], expected))
            {
                failures.Add("The host must bind only the canonical internal listener options section.");
            }
        }
    }

    private static bool IsFixtureGatewayWaitRemoval(InvocationExpressionSyntax invocation)
    {
        // Fixture bootstrap replaces one SDK gateway wait so metadata discovery
        // can finish. It does not alter endpoint or environment annotations.
        ForEachStatementSyntax? loop = invocation.Ancestors().OfType<ForEachStatementSyntax>().FirstOrDefault();
        StatementSyntax supported = SyntaxFactory.ParseStatement("""
            foreach (WaitAnnotation wait in timesheets.Resource.Annotations.OfType<WaitAnnotation>()
                .Where(wait => ReferenceEquals(wait.Resource, eventStore.Resource)).ToArray())
            {
                timesheets.Resource.Annotations.Remove(wait);
            }
            """);
        IfStatementSyntax[] guards = loop?.Ancestors().OfType<IfStatementSyntax>()
            .Where(static conditional => conditional.Condition is IdentifierNameSyntax
                { Identifier.ValueText: "fixtureEnabled" }).ToArray() ?? [];
        return loop is not null && SyntaxFactory.AreEquivalent(loop, supported) && guards.Length > 0
            && guards.All(conditional => conditional.Statement.Span.Contains(loop.Span));
    }
}
