using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Hexalith.Timesheets.Contracts.Models.MagicLinks;
using Hexalith.Timesheets.Server.MagicLinks;

using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

namespace Hexalith.Timesheets.IntegrationTests;

public sealed partial class MagicLinkConfirmationHttpBoundaryTests
{
    /// <summary>Proves scopes and complete exception chains reach the privacy assertions.</summary>
    /// <param name="field">The sole captured field carrying the protected value.</param>
    [Theory]
    [InlineData("scalar-scope")]
    [InlineData("structured-scope")]
    [InlineData("exception")]
    [InlineData("inner-exception")]
    public void LoggerPrivacyChecksCaptureScopesAndExceptions(string field)
    {
        const string Secret = "query-secret-probe";
        using CapturingLoggerProvider provider = new();
        using ILoggerFactory loggers = LoggerFactory.Create(builder => builder.AddProvider(provider));
        ILogger logger = loggers.CreateLogger("ThirdParty.Diagnostics");
        using (logger.BeginScope("outer-scope"))
        using (logger.BeginScope(field == "scalar-scope" ? Secret : "inner-scope"))
        using (logger.BeginScope(new Dictionary<string, object?>
        {
            ["Probe"] = field == "structured-scope" ? Secret : "safe-value"
        }))
        {
            Exception? exception = field switch
            {
                "exception" => new InvalidOperationException(Secret),
                "inner-exception" => new InvalidOperationException("outer-failure", new ArgumentException(Secret)),
                _ => null
            };
            logger.LogWarning(exception, "safe-message");
        }

        LogRecord captured = provider.Records.ShouldHaveSingleItem();
        captured.Scopes.ShouldContain("outer-scope");
        captured.Scopes.Length.ShouldBe(3);
        Should.Throw<ShouldAssertException>(() => AssertSensitiveDiagnosticsAbsent(captured, Secret));
        if (field.Contains("exception", StringComparison.Ordinal))
        {
            captured.Exception.ShouldNotBeNull().ShouldContain(Secret);
            captured.Exception.ShouldContain(nameof(InvalidOperationException));
        }

        logger.LogWarning("after-disposal");
        provider.Records.Last().Scopes.ShouldBeEmpty();
        AssertSensitiveDiagnosticsAbsent(provider.Records.Last(), Secret);
    }

    /// <summary>Proves asynchronous scope ownership is isolated and restored after disposal.</summary>
    [Fact]
    public async Task LoggerScopesRemainIsolatedAcrossConcurrentOperations()
    {
        using CapturingLoggerProvider provider = new();
        using ILoggerFactory loggers = LoggerFactory.Create(builder => builder.AddProvider(provider));
        ILogger logger = loggers.CreateLogger("ThirdParty.Diagnostics");
        await Task.WhenAll(CaptureAsync("first"), CaptureAsync("second"));
        provider.Records.Count.ShouldBe(2);
        foreach (LogRecord record in provider.Records)
        {
            record.Scopes.ShouldHaveSingleItem().ShouldBe(record.Message);
        }

        async Task CaptureAsync(string identity)
        {
            using (logger.BeginScope(identity))
            {
                await Task.Yield();
                logger.LogWarning("{Identity}", identity);
            }
        }
    }

    /// <summary>Separates permitted diagnostic metadata from the known duration sentinel.</summary>
    /// <param name="field">The diagnostic or response field under test.</param>
    /// <param name="content">The candidate text.</param>
    /// <param name="shouldReject">Whether duration evidence must be rejected.</param>
    [Theory]
    [InlineData("state", "CorrelationId=request-60", false)]
    [InlineData("scope", "RequestId=request-60", false)]
    [InlineData("state", "TimestampUtc=2026-06-19T13:30:00.60+00:00", false)]
    [InlineData("message", "External link denial emitted with category Unknown at 2026-06-19T13:30:00.60+00:00 for correlation request-60.", false)]
    [InlineData("response", "{\"traceId\":\"request-60\",\"status\":403}", false)]
    [InlineData("response", "{\"timestampUtc\":\"2026-06-19T13:30:00.60+00:00\"}", false)]
    [InlineData("message", "60", true)]
    [InlineData("state", "Minutes=60", true)]
    [InlineData("scope", "Minutes=60", true)]
    [InlineData("exception", "Recorded effort was 60", true)]
    [InlineData("state", "CorrelationId=request-60 Minutes=60", true)]
    [InlineData("state", "Evidence=CorrelationId=60", true)]
    [InlineData("scope", "Evidence=CorrelationId=60", true)]
    [InlineData("message", "Evidence=CorrelationId=60", true)]
    [InlineData("exception", "CorrelationId=60", true)]
    [InlineData("message", "60min", true)]
    [InlineData("message", "PT60M", true)]
    [InlineData("message", "Recorded effort was 60 for correlation request-60.", true)]
    [InlineData("response", "{\"traceId\":\"request-60\",\"minutes\":60}", true)]
    [InlineData("response", "{\"traceId\":\"request-60\",\"minutes\":\"60\"}", true)]
    [InlineData("response", "{\"traceId\":\"request-60\",\"detail\":\"60\"}", true)]
    [InlineData("response", "{\"traceId\":\"party-1\"}", true)]
    [InlineData("response", "{\"traceId\":\"request-60\",\"minutes\":6e1}", true)]
    [InlineData("response", "{\"traceId\":\"Recorded effort was 60\"}", true)]
    public void DurationPrivacyAssertionsDistinguishDiagnosticMetadata(string field, string content, bool shouldReject)
    {
        LogRecord record = new("Hexalith.Timesheets.Tests", field == "message" ? content : "safe",
            field == "state" ? [content] : [])
        {
            Scopes = field == "scope" ? [content] : [],
            Exception = field == "exception" ? content : null
        };
        Action assertion = field == "response"
            ? () => AssertSensitiveResponseMaterialAbsent(content, "query-secret-probe")
            : () => AssertSensitiveDiagnosticsAbsent(record, "query-secret-probe");
        if (shouldReject)
        {
            Should.Throw<ShouldAssertException>(assertion);
        }
        else
        {
            assertion();
        }
    }

    /// <summary>Scans both header collections, including names and normally variable Date values.</summary>
    /// <param name="location">The response or content header carrying the sole sentinel.</param>
    /// <param name="material">The protected sentinel.</param>
    [Theory]
    [MemberData(nameof(ProtectedHeaderCases))]
    public async Task ResponsePrivacyChecksRejectProtectedHeaders(string location, string material)
    {
        using HttpResponseMessage response = OpaqueDenialResponse();
        bool added = location switch
        {
            "response-name" => response.Headers.TryAddWithoutValidation($"X-{material}", "safe"),
            "content-name" => response.Content.Headers.TryAddWithoutValidation($"X-{material}", "safe"),
            "response-value" => response.Headers.TryAddWithoutValidation("X-Probe", material),
            "content-value" => response.Content.Headers.TryAddWithoutValidation("X-Probe", material),
            "date" => response.Headers.TryAddWithoutValidation("Date", material),
            _ => throw new ArgumentOutOfRangeException(nameof(location))
        };
        added.ShouldBeTrue();
        await Assert.ThrowsAsync<ShouldAssertException>(() => CaptureFailureAsync(response, location, "query-secret-probe"));
    }

    /// <summary>Supplies independent protected values in each HTTP header channel.</summary>
    /// <returns>Header locations and protected-value sentinels.</returns>
    public static IEnumerable<object[]> ProtectedHeaderCases()
    {
        foreach (string material in new[]
        {
            "query-secret-probe", Hash("query-secret-probe"), "tenant-1", "tenant-2", "party-1", "party-2",
            "project-1", "work-1", "time-entry-1", "time-entry-2", "sensitive customer comment"
        })
        {
            foreach (string location in new[] { "response-name", "content-name", "response-value", "content-value", "date" })
            {
                // Header names cannot contain whitespace; its value channels still cover comment text.
                if (!location.EndsWith("name", StringComparison.Ordinal) || !material.Contains(' '))
                {
                    yield return [location, material.ToUpperInvariant()];
                }
            }
        }
    }

    /// <summary>Keeps routine protocol vocabulary and numeric lengths distinct from business evidence.</summary>
    [Fact]
    public async Task ResponsePrivacyChecksAllowProtocolHeaders()
    {
        using HttpResponseMessage response = OpaqueDenialResponse();
        response.Content.Headers.ContentLength = 60;
        response.Headers.TryAddWithoutValidation("X-Correlation-Id", "request-60").ShouldBeTrue();
        response.Headers.TryAddWithoutValidation("X-Token-Policy", "no-store").ShouldBeTrue();
        response.Headers.Date = new DateTimeOffset(2060, 6, 19, 13, 30, 0, TimeSpan.Zero);
        await CaptureFailureAsync(response, "safe-headers", "query-secret-probe");
    }

    /// <summary>Rejects identical business-value leaks that header equivalence cannot detect.</summary>
    /// <param name="name">The custom response header name.</param>
    /// <param name="value">The disclosed business value.</param>
    [Theory]
    [InlineData("X-Duration-Minutes", "60")]
    [InlineData("X-Approval-State", "Draft")]
    [InlineData("X-Activity-Label", "Delivery")]
    public async Task ResponsePrivacyChecksRejectBusinessHeaders(string name, string value)
    {
        using HttpResponseMessage response = OpaqueDenialResponse();
        response.Headers.TryAddWithoutValidation(name, value).ShouldBeTrue();
        await Assert.ThrowsAsync<ShouldAssertException>(() => CaptureFailureAsync(response, name, "query-secret-probe"));
    }

    /// <summary>Malformed or duplicate JSON must not prevent raw protected-value or header scans.</summary>
    /// <param name="body">The malformed or ambiguous JSON body.</param>
    [Theory]
    [InlineData("{\"traceId\":\"first\",\"traceId\":\"second\",\"detail\":\"query-secret-probe\"}")]
    [InlineData("{\"traceId\":\"query-secret-probe\"")]
    public async Task MalformedResponseJsonStillChecksProtectedMaterial(string body)
    {
        using HttpResponseMessage response = OpaqueDenialResponse(HttpStatusCode.InternalServerError);
        response.Content = new StringContent(body);
        ShouldAssertException failure = await Assert.ThrowsAsync<ShouldAssertException>(() =>
            CaptureFailureAsync(response, "malformed-response", "query-secret-probe"));
        failure.Message.ShouldContain("query-secret-probe");
    }

    /// <summary>Duplicate diagnostic fields must not prevent protected header checks.</summary>
    [Fact]
    public async Task DuplicateResponseJsonStillChecksProtectedHeaders()
    {
        using HttpResponseMessage response = OpaqueDenialResponse(HttpStatusCode.InternalServerError);
        response.Content = new StringContent("{\"traceId\":\"first\",\"traceId\":\"second\"}");
        response.Headers.TryAddWithoutValidation("X-Probe", "query-secret-probe").ShouldBeTrue();
        ShouldAssertException failure = await Assert.ThrowsAsync<ShouldAssertException>(() =>
            CaptureFailureAsync(response, "duplicate-response", "query-secret-probe"));
        failure.Message.ShouldContain("query-secret-probe");
    }

    /// <summary>Framework error bodies retain response-specific tenant exclusions without banning type vocabulary.</summary>
    /// <param name="tenant">The protected tenant identifier.</param>
    [Theory]
    [InlineData("tenant-1")]
    [InlineData("TENANT-2")]
    public void ErrorResponsePrivacyRejectsTenantIdentifiers(string tenant)
        => Should.Throw<ShouldAssertException>(() => AssertProtectedResponseMaterialAbsent(
            $"System.Threading.CancellationToken {tenant}", "query-secret-probe"));

    /// <summary>Proves privacy scans run before status, content-type and JSON-shape checks.</summary>
    /// <param name="status">An unexpected HTTP status.</param>
    /// <param name="location">The sole protected response channel.</param>
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "body")]
    [InlineData(HttpStatusCode.Unauthorized, "header")]
    [InlineData(HttpStatusCode.InternalServerError, "body")]
    [InlineData(HttpStatusCode.InternalServerError, "header")]
    public async Task UnexpectedStatusStillChecksResponsePrivacyFirst(HttpStatusCode status, string location)
    {
        const string Secret = "query-secret-probe";
        using HttpResponseMessage response = OpaqueDenialResponse(status);
        if (location == "body")
        {
            response.Content = new StringContent(Secret);
        }
        else
        {
            response.Headers.TryAddWithoutValidation("X-Probe", Secret).ShouldBeTrue();
        }

        ShouldAssertException failure = await Assert.ThrowsAsync<ShouldAssertException>(() =>
            CaptureFailureAsync(response, "unexpected-status", Secret));
        failure.Message.ShouldContain(Secret);
        failure.Message.ShouldNotContain("Forbidden");
    }

    /// <summary>Retains the status gate even when an unexpected response is privacy-safe.</summary>
    [Fact]
    public async Task SafeUnexpectedStatusStillFailsTheForbiddenGate()
    {
        using HttpResponseMessage response = OpaqueDenialResponse(HttpStatusCode.Unauthorized);
        ShouldAssertException failure = await Assert.ThrowsAsync<ShouldAssertException>(() =>
            CaptureFailureAsync(response, "safe-unexpected-status", "query-secret-probe"));
        failure.Message.ShouldContain("Forbidden");
    }

    /// <summary>Exercises exception middleware through every real external route without disclosing query values.</summary>
    [Fact]
    public async Task ThrowingMagicLinkDependencyKeepsQueryValuesOutOfDiagnostics()
    {
        IMagicLinkConfirmationCapabilityStateLoader loader = Substitute.For<IMagicLinkConfirmationCapabilityStateLoader>();
        loader.LoadTokenStateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromException<MagicLinkEndpointTokenState>(new InvalidOperationException("dependency-failure-probe")));
        using MagicLinkHttpBoundaryFactory factory = new(stateLoader: loader);
        using HttpClient client = factory.CreateClient();
        List<string> tokens = [];
        foreach (ExternalRoute route in ExternalRoutes())
        {
            string token = $"{route.Name}-query-secret-probe";
            tokens.Add(token);
            using HttpResponseMessage response = await SendAsync(client, route, token);
            string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            AssertProtectedResponseMaterialAbsent(body, token);
            AssertSensitiveResponseHeadersAbsent(response, token);
            foreach (LogRecord record in factory.Logs.Records)
            {
                foreach (string observedToken in tokens)
                {
                    AssertSensitiveDiagnosticsAbsent(record, observedToken);
                }
            }

            response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
        }

        factory.Gateway.SubmissionCount.ShouldBe(0);
        factory.Logs.Records.Count(record => record.Level >= LogLevel.Error
            && record.Exception?.Contains("dependency-failure-probe", StringComparison.Ordinal) == true).ShouldBe(4);
        await loader.Received(4).LoadTokenStateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static HttpResponseMessage OpaqueDenialResponse(HttpStatusCode status = HttpStatusCode.Forbidden)
        => new(status)
        {
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                title = MagicLinkInvalidLinkDenial.Default.Title,
                detail = MagicLinkInvalidLinkDenial.Default.Detail,
                status = (int)status
            }), System.Text.Encoding.UTF8, "application/problem+json")
        };

    private static void AssertSensitiveResponseHeadersAbsent(HttpResponseMessage response, string token)
    {
        foreach ((string name, IEnumerable<string> values) in response.Headers.Concat(response.Content.Headers))
        {
            foreach (string content in values.Prepend(name))
            {
                AssertProtectedResponseMaterialAbsent(content, token);
                foreach (string businessValue in new[] { "durationMinutes", "Draft", "Delivery", "RecoveryPath" })
                {
                    content.ShouldNotContain(businessValue, Case.Insensitive);
                }

                if (!IsNumericProtocolHeader(name))
                {
                    AssertDurationEvidenceAbsent(content);
                }
            }
        }
    }

    private static void AssertProtectedResponseMaterialAbsent(string content, string token)
    {
        AssertProtectedMaterialAbsent(content, token);
        content.ShouldNotContain("tenant-1", Case.Insensitive);
        content.ShouldNotContain("tenant-2", Case.Insensitive);
    }

    private static bool IsNumericProtocolHeader(string name)
        => new[] { "Content-Length", "Date", "Age", "Expires", "Last-Modified", "Retry-After",
            "Cache-Control", "Content-Type", "traceparent", "X-Correlation-Id", "Request-Id" }
            .Contains(name, StringComparer.OrdinalIgnoreCase);

    private static void AssertDurationEvidenceAbsent(string content)
        => Regex.IsMatch(content, @"(?<!\d)60(?!\d)",
            RegexOptions.CultureInvariant).ShouldBeFalse("Duration evidence must not appear outside permitted metadata fields.");

    private static void AssertDiagnosticDurationEvidenceAbsent(LogRecord record)
    {
        AssertDurationEvidenceAbsent(record.Category);
        foreach (string field in record.State.Concat(record.Scopes))
        {
            int delimiter = field.IndexOf('=');
            if (delimiter < 0 || !IsPermittedNumericMetadata(field[..delimiter], field[(delimiter + 1)..]))
            {
                AssertDurationEvidenceAbsent(field);
            }
        }

        // The known rendered denial has explicit metadata positions. Arbitrary prose, exception text,
        // and ordinary field values containing metadata-like text receive no exemption.
        Match denial = Regex.Match(record.Message,
            @"\AExternal link denial emitted with category (?<category>\S+) at (?<timestamp>.+) for correlation (?<correlation>[\w.:\-]+)\.\z",
            RegexOptions.CultureInvariant);
        if (denial.Success
            && IsPermittedNumericMetadata("TimestampUtc", denial.Groups["timestamp"].Value)
            && IsPermittedNumericMetadata("CorrelationId", denial.Groups["correlation"].Value))
        {
            AssertDurationEvidenceAbsent(denial.Groups["category"].Value);
        }
        else
        {
            AssertDurationEvidenceAbsent(record.Message);
        }

        AssertDurationEvidenceAbsent(record.Exception ?? string.Empty);
    }

    private static bool IsPermittedNumericMetadata(string name, string value)
        => name.Equals("TimestampUtc", StringComparison.OrdinalIgnoreCase)
            ? DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
            : new[] { "CorrelationId", "TraceId", "SpanId", "RequestId" }.Contains(name, StringComparer.OrdinalIgnoreCase)
                && Regex.IsMatch(value, @"\A[\w.:\-]+\z", RegexOptions.CultureInvariant);

    private static void AssertResponseDurationEvidenceAbsent(string content)
    {
        try
        {
            JsonNode? node = JsonNode.Parse(content);
            if (node is JsonObject problem)
            {
                foreach (string field in new[] { "traceId", "correlationId", "timestampUtc" })
                {
                    if (problem[field] is JsonValue value && value.TryGetValue(out string? metadata)
                        && metadata is not null && IsPermittedNumericMetadata(field, metadata))
                    {
                        problem[field] = "";
                    }
                }
            }

            AssertJsonDurationAbsent(node);
            AssertDurationEvidenceAbsent(node?.ToJsonString() ?? content);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            // Duplicate properties and malformed JSON must not abort raw privacy checks or header scans.
            AssertDurationEvidenceAbsent(content);
        }
    }

    private static void AssertJsonDurationAbsent(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue(out double number))
        {
            number.ShouldNotBe(60d, "Numeric duration evidence includes exponent representations.");
        }
        else if (node is JsonObject problem)
        {
            foreach ((_, JsonNode? child) in problem)
            {
                AssertJsonDurationAbsent(child);
            }
        }
        else if (node is JsonArray items)
        {
            foreach (JsonNode? item in items)
            {
                AssertJsonDurationAbsent(item);
            }
        }
    }
}
