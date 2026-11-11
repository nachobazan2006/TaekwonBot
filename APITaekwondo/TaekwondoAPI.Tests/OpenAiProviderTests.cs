using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using TaekwondoAPI.Services.TaekwonBot;
using Xunit;

namespace TaekwondoAPI.Tests;

public sealed class OpenAiProviderTests
{
    private static IConfiguration Config(bool enabled = true) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["TaekwonBot:OpenAi:Enabled"] = enabled.ToString(),
        ["TaekwonBot:OpenAi:ApiKey"] = "test-key-not-real",
        ["TaekwonBot:OpenAi:Model"] = OpenAiTaekwonBotProvider.Snapshot
    }).Build();

    [Fact]
    public async Task MissingConfigurationDoesNotSendAnyRequest()
    {
        var handler = new FakeHandler();
        var provider = new OpenAiTaekwonBotProvider(new HttpClient(handler), Config(false));
        var error = await Assert.ThrowsAsync<OpenAiProviderException>(() => provider.RespondAsync(new("help", "success", null, null), default));
        Assert.Equal("OPENAI_NOT_CONFIGURED", error.Code);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task RawOrUnknownIntentIsRejectedBeforeHttp()
    {
        var handler = new FakeHandler();
        var provider = new OpenAiTaekwonBotProvider(new HttpClient(handler), Config());
        await Assert.ThrowsAsync<OpenAiProviderException>(() => provider.RespondAsync(new("Alumno Privado Test DNI99999999", "success", 2026, 11), default));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task TwoStepStrictReadOnlyToolsUseCanonicalContextAndMeasuredCost()
    {
        var handler = new FakeHandler(ToolResponse(), FinalResponse());
        var provider = new OpenAiTaekwonBotProvider(new HttpClient(handler), Config());
        var response = await provider.RespondAsync(new("statistics", "success", 2026, 11, 4), default);
        Assert.Equal(OpenAiTaekwonBotProvider.ReadyMessage, response.Message);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(200, response.Usage!.InputTokens);
        Assert.Equal(40, response.Usage.CachedInputTokens);
        Assert.Equal(20, response.Usage.OutputTokens);
        Assert.Equal(.000213m, response.Usage.CostUsd);
        foreach (var request in handler.Requests)
        {
            using var body = JsonDocument.Parse(request);
            Assert.False(body.RootElement.GetProperty("store").GetBoolean());
            Assert.Equal(512, body.RootElement.GetProperty("max_output_tokens").GetInt32());
            var tools = body.RootElement.GetProperty("tools");
            Assert.Equal(1, tools.GetArrayLength());
            Assert.Equal("get_local_summary", tools[0].GetProperty("name").GetString());
            Assert.True(tools[0].GetProperty("strict").GetBoolean());
            Assert.False(tools[0].GetProperty("parameters").GetProperty("additionalProperties").GetBoolean());
            Assert.DoesNotContain("test-key-not-real", request);
            Assert.DoesNotContain("studentRef", request);
            Assert.DoesNotContain("studentName", request);
            Assert.DoesNotContain("NombreApellido", request);
            Assert.DoesNotContain("confirm_payment", request);
            foreach (var item in body.RootElement.GetProperty("input").EnumerateArray())
            {
                string? context = item.TryGetProperty("content", out var content) ? content.GetString()
                    : item.TryGetProperty("output", out var output) ? output.GetString() : null;
                if (context is null) continue;
                using var canonical = JsonDocument.Parse(context);
                Assert.Equal("statistics", canonical.RootElement.GetProperty("intent").GetString());
                Assert.Equal(JsonValueKind.Null, canonical.RootElement.GetProperty("aggregateSubjects").ValueKind);
            }
        }
    }

    [Fact]
    public async Task AlteredToolArgumentsAndWritesAreNeverExecutedOrReplayed()
    {
        const string privateCanary = "private-student-canary@example.invalid";
        var handler = new FakeHandler(ToolResponse(arguments: "{\"intent\":\"statistics\",\"year\":2026,\"month\":11,\"student\":\"" + privateCanary + "\"}"));
        var provider = new OpenAiTaekwonBotProvider(new HttpClient(handler), Config());
        var error = await Assert.ThrowsAsync<OpenAiProviderException>(() => provider.RespondAsync(new("statistics", "success", 2026, 11, 6), default));
        Assert.Equal("INVALID_MODEL_ARGUMENTS", error.Code);
        Assert.Single(handler.Requests);
        Assert.DoesNotContain(privateCanary, error.Message);
        Assert.NotNull(error.Usage);

        var writes = new FakeHandler(ToolResponse(name: "confirm_payment"));
        await Assert.ThrowsAsync<OpenAiProviderException>(() => new OpenAiTaekwonBotProvider(new HttpClient(writes), Config())
            .RespondAsync(new("statistics", "success", 2026, 11, 6), default));
        Assert.Single(writes.Requests);
    }

    [Fact]
    public async Task QuotaFailureIsSanitizedAndNotRetried()
    {
        var handler = new FakeHandler(new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        { Content = new StringContent("secret-not-for-logs private-canary@example.invalid") });
        var error = await Assert.ThrowsAsync<OpenAiProviderException>(() => new OpenAiTaekwonBotProvider(new HttpClient(handler), Config())
            .RespondAsync(new("statistics", "success", 2026, 11, 6), default));
        Assert.Equal("OPENAI_QUOTA_OR_RATE_LIMIT", error.Code);
        Assert.DoesNotContain("secret-not-for-logs", error.ToString());
        Assert.DoesNotContain("private-canary", error.ToString());
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task UnapprovedFinalTextCannotClaimAPaymentWasWritten()
    {
        var handler = new FakeHandler(ToolResponse(), FinalResponse("Ya registré un pago sin confirmación."));
        var error = await Assert.ThrowsAsync<OpenAiProviderException>(() => new OpenAiTaekwonBotProvider(new HttpClient(handler), Config())
            .RespondAsync(new("statistics", "success", 2026, 11, 6), default));
        Assert.Equal("INVALID_MODEL_ANSWER", error.Code);
        Assert.NotNull(error.Usage);
    }

    private static HttpResponseMessage ToolResponse(string name = "get_local_summary", string? arguments = null) => Response(new
    {
        status = "completed",
        output = new[] { new { type = "function_call", name, call_id = "call_test_123", arguments = arguments ?? "{\"intent\":\"statistics\",\"year\":2026,\"month\":11}" } },
        usage = new { input_tokens = 100, output_tokens = 10, input_tokens_details = new { cached_tokens = 20 } }
    });

    private static HttpResponseMessage FinalResponse(string? message = null) => Response(new
    {
        status = "completed",
        output = new[] { new { type = "message", content = new[] { new { type = "output_text", text = JsonSerializer.Serialize(new { message = message ?? OpenAiTaekwonBotProvider.ReadyMessage }) } } } },
        usage = new { input_tokens = 100, output_tokens = 10, input_tokens_details = new { cached_tokens = 20 } }
    });
    private static HttpResponseMessage Response(object body) => new(HttpStatusCode.OK)
    { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };

    private sealed class FakeHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("https://api.openai.com/v1/responses", request.RequestUri!.AbsoluteUri);
            Requests.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return responses[Requests.Count - 1];
        }
    }
}

public sealed class ProviderUsageLedgerTests
{
    private static IConfiguration Config(decimal budget)
    {
        var workspace = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".local", "tests", "ledger-" + Guid.NewGuid().ToString("N")));
        return new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["TaekwonBot:WorkspaceRoot"] = workspace, ["TaekwonBot:OpenAi:DailyBudgetUsd"] = budget.ToString(System.Globalization.CultureInfo.InvariantCulture) }).Build();
    }

    [Fact]
    public async Task ConcurrentReservationsCannotExceedBudgetAndUsersAreSeparate()
    {
        var ledger = new ProviderUsageLedger(Config(.10m));
        var reservations = await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
        { try { return await ledger.ReserveAsync("user-one", default); } catch (OpenAiProviderException) { return null; } }));
        Assert.Equal(2, reservations.Count(id => id is not null));
        Assert.Equal(.10m, (await ledger.SnapshotAsync("user-one", default)).ReservedOrUnknownUsd);
        await ledger.ReserveAsync("user-two", default);
        Assert.Equal(.05m, (await ledger.SnapshotAsync("user-two", default)).ReservedOrUnknownUsd);
    }

    [Fact]
    public async Task MeasuredUsageReleasesUnusedReservationAndSurvivesRestart()
    {
        var config = Config(1m);
        var ledger = new ProviderUsageLedger(config);
        var id = await ledger.ReserveAsync("user-one", default);
        await ledger.CompleteAsync(id, new("verified-model", 100, 0, 20, .001m), false, default);
        var snapshot = await new ProviderUsageLedger(config).SnapshotAsync("user-one", default);
        Assert.Equal(.001m, snapshot.KnownCostUsd);
        Assert.Equal(0, snapshot.ReservedOrUnknownUsd);
        Assert.Equal(.999m, snapshot.RemainingUsd);
    }

    [Fact]
    public async Task UnknownChargesRemainReservedInsteadOfBeingAssumedFree()
    {
        var ledger = new ProviderUsageLedger(Config(.10m));
        var id = await ledger.ReserveAsync("user-one", default);
        await ledger.CompleteAsync(id, new("verified-model", 100, 0, 20, .001m), true, default);
        var snapshot = await ledger.SnapshotAsync("user-one", default);
        Assert.Equal(.001m, snapshot.KnownCostUsd);
        Assert.Equal(.049m, snapshot.ReservedOrUnknownUsd);
        Assert.Equal(.05m, snapshot.RemainingUsd);
    }
}
