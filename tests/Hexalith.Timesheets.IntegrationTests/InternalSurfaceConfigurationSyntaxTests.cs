using Shouldly;

namespace Hexalith.Timesheets.IntegrationTests;

public sealed class InternalSurfaceConfigurationSyntaxTests
{
    [Fact]
    public void HealthyProductionWiringPasses()
    {
        InternalSurfaceConfigurationSyntax.Validate(AppHost(), Host()).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(""".WithArgs("--Timesheets:InternalSurface:Port=8080")""")]
    [InlineData(""".WithArgs("--Timesheets:Internal\u0053urface:Port=8080")""")]
    [InlineData(""".WithArgs("--Timesheets:Internal" + "Surface:Port=8080")""")]
    [InlineData(""".WithEnvironment("Timesheets__Internal\u0053urface__Port", "8080")""")]
    public void DecodedAndComputedListenerOverridesFail(string mutation)
    {
        InternalSurfaceConfigurationSyntax.Validate(AppendToResource(mutation), Host()).ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData(""".WithArgs(GetArguments())""")]
    [InlineData(""".WithArgs($"--{setting}=8080")""")]
    [InlineData(""".WithEnvironment(GetSetting(), "8080")""")]
    [InlineData(""".ConfigureUnknownListener()""")]
    public void UnsupportedExpressionsWithinCheckedConfigurationFailClosed(string mutation)
    {
        InternalSurfaceConfigurationSyntax.Validate(AppendToResource(mutation), Host()).ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData("AllowOnAnyPort")]
    [InlineData("""AllowOnAny\u0050ort""")]
    public void DecodedBypassPropertyAssignmentsFail(string property)
    {
        string host = Host().Replace("WebApplication app = builder.Build();",
            "builder.Services.Configure<InternalSurfaceOptions>(o => o." + property
            + " = true);\nWebApplication app = builder.Build();", StringComparison.Ordinal);
        InternalSurfaceConfigurationSyntax.Validate(AppHost(), host).ShouldNotBeEmpty();
    }

    [Fact]
    public void InternalListenerExportMustBelongToTimesheets()
    {
        const string export = ".WithEnvironment(\n        \"Timesheets__InternalSurface__Port\",\n        InternalPort.ToString(System.Globalization.CultureInfo.InvariantCulture))";
        string appHost = AppHost();
        appHost.ShouldContain(export);
        appHost = appHost.Replace(export, string.Empty, StringComparison.Ordinal);
        int gateway = appHost.IndexOf(".AddHexalithEventStoreGatewayProject()", StringComparison.Ordinal);
        gateway.ShouldBeGreaterThan(0);
        appHost = appHost.Insert(appHost.IndexOf(';', gateway), export);
        InternalSurfaceConfigurationSyntax.Validate(appHost, Host()).ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData("int InternalPort = 8081;")]
    [InlineData("int InternalPort = builder.Configuration.GetValue<int>(\"ListenerPort\");")]
    [InlineData("const int InternalPort = 8080;")]
    public void InternalListenerDeclarationMustBeImmutable8081(string declaration)
    {
        string appHost = AppHost().Replace("const int InternalPort = 8081;", declaration, StringComparison.Ordinal);
        appHost.ShouldContain(declaration);
        InternalSurfaceConfigurationSyntax.Validate(appHost, Host()).ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData("builder.Services.PostConfigure<InternalSurfaceOptions>(o => o.Port = 8080);")]
    [InlineData("builder.Services.Configure<Hexalith.Timesheets.Runtime.InternalSurfaceOptions>(o => o.Port = 8080);")]
    [InlineData("builder.Services.PostConfigure<Hexalith.Timesheets.Runtime.InternalSurfaceOptions>(o => o.Port = 8080);")]
    public void ExtraAndQualifiedOptionsBindingsAreRejected(string binding)
    {
        string host = Host().Replace("WebApplication app = builder.Build();", binding
            + "\nWebApplication app = builder.Build();", StringComparison.Ordinal);
        InternalSurfaceConfigurationSyntax.Validate(AppHost(), host).ShouldNotBeEmpty();
    }

    [Fact]
    public void DirectPostInitializerAnnotationMutationIsRejected()
    {
        const string mutation = """
            timesheets.Resource.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
                context.EnvironmentVariables[string.Concat("Timesheets__Internal", "Surface__Port")] = "8080"));
            """;
        string appHost = AppHost();
        int start = appHost.IndexOf(".AddProject<Projects.Hexalith_Timesheets>", StringComparison.Ordinal);
        appHost = appHost.Insert(appHost.IndexOf(';', start) + 1, "\n" + mutation);
        InternalSurfaceConfigurationSyntax.Validate(appHost, Host()).ShouldNotBeEmpty();
    }

    [Theory]
    [InlineData("WaitAnnotation", "EnvironmentCallbackAnnotation")]
    [InlineData(".Remove(wait)", ".Clear()")]
    [InlineData("ReferenceEquals(wait.Resource, eventStore.Resource)", "true")]
    [InlineData("if (fixtureEnabled)", "if (true)")]
    public void OnlyTheFixtureGatewayWaitRemovalIsAccepted(string original, string replacement)
    {
        string appHost = AppHost();
        appHost.ShouldContain(original);
        appHost = appHost.Replace(original, replacement, StringComparison.Ordinal);
        InternalSurfaceConfigurationSyntax.Validate(appHost, Host()).ShouldNotBeEmpty();
    }

    [Fact]
    public void FixtureGatewayWaitRemovalInElseBranchIsRejected()
    {
        string appHost = AppHost().Replace("if (fixtureEnabled)",
            "if (fixtureEnabled) { } else", StringComparison.Ordinal);
        InternalSurfaceConfigurationSyntax.Validate(appHost, Host()).ShouldNotBeEmpty();
    }

    [Fact]
    public void HarmlessCommentsAndConstantArgumentsPass()
    {
        string appHost = AppendToResource(""".WithArgs("--Harmless=" + "value")""")
            + "\n// .WithArgs(\"--Timesheets:InternalSurface:Port=8080\") AllowOnAnyPort\n";
        string host = Host() + "\n/* o.AllowOnAny\\u0050ort = true; */\n";
        InternalSurfaceConfigurationSyntax.Validate(appHost, host).ShouldBeEmpty();
    }

    private static string AppendToResource(string mutation)
    {
        string appHost = AppHost();
        int start = appHost.IndexOf(".AddProject<Projects.Hexalith_Timesheets>", StringComparison.Ordinal);
        start.ShouldBeGreaterThan(0);
        return appHost.Insert(appHost.IndexOf(';', start), mutation);
    }

    private static string AppHost() => File.ReadAllText(TestRepositoryRoot.PathTo("src", "Hexalith.Timesheets.AppHost", "Program.cs"));

    private static string Host() => File.ReadAllText(TestRepositoryRoot.PathTo("src", "Hexalith.Timesheets", "Program.cs"));
}
