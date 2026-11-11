using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TaekwondoAPI.Data;
using TaekwondoAPI.Services.TaekwonBot;

namespace TaekwondoAPI.Tests;

public sealed class ConversationTests
{
    private static IConfiguration Config() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["TaekwonBot:OpenAi:Enabled"] = "true",
        ["TaekwonBot:OpenAi:ApiKey"] = "test-only-key",
        ["TaekwonBot:OpenAi:Model"] = OpenAiTaekwonBotProvider.Snapshot,
        ["TaekwonBot:OpenAi:DailyBudgetUsd"] = "1",
        ["TaekwonBot:WorkspaceRoot"] = Path.Combine(Path.GetTempPath(), "taekwonbot-conversation-" + Guid.NewGuid().ToString("N"))
    }).Build();

    [Fact]
    public async Task ModelCannotInventMissingPaymentValues()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var db = new TaekwondoDbContext(new DbContextOptionsBuilder<TaekwondoDbContext>().UseSqlite(connection).Options);
        await db.Database.MigrateAsync();
        var clock = new FixedClock();
        await LocalDatabase.SeedAsync(db, clock);
        var config = Config();
        var handler = new ForgedPaymentHandler();
        var engine = new OpenAiConversationEngine(new HttpClient(handler), config, new ProviderUsageLedger(config));
        var bot = new BotService(db, new StudentReferenceStore(clock), clock, new OpenAiNamedProvider(), config, engine, new ConversationStateStore());
        var response = await bot.Chat(new("Cargale una cuota a Rio"), "user-a", "session-a", default);
        Assert.Equal("answer", response.State);
        Assert.False(response.ConfirmationRequired);
        Assert.Null(response.PendingOperation);
        Assert.Equal(2, await db.Cuotas.CountAsync());
        Assert.Equal(0, await db.BotOperations.CountAsync());
        Assert.Contains("mes, importe o medio de pago", handler.LastToolOutput);
    }

    [Fact]
    public async Task UnbackedModelWriteClaimIsReplaced()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var db = new TaekwondoDbContext(new DbContextOptionsBuilder<TaekwondoDbContext>().UseSqlite(connection).Options);
        await db.Database.MigrateAsync();
        var clock = new FixedClock();
        await LocalDatabase.SeedAsync(db, clock);
        var config = Config();
        var handler = new ForgedPaymentHandler(directClaim: true);
        var bot = new BotService(db, new StudentReferenceStore(clock), clock, new OpenAiNamedProvider(), config,
            new OpenAiConversationEngine(new HttpClient(handler), config, new ProviderUsageLedger(config)), new ConversationStateStore());
        var response = await bot.Chat(new("Hola"), "user-a", "session-a", default);
        Assert.DoesNotContain("registré un pago", response.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(2, await db.Cuotas.CountAsync());
    }

    [Fact]
    public async Task MockChatPreparesCorrectsAndOnlyWritesAfterConfirmation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var db = new TaekwondoDbContext(new DbContextOptionsBuilder<TaekwondoDbContext>().UseSqlite(connection).Options);
        await db.Database.MigrateAsync();
        var clock = new FixedClock();
        await LocalDatabase.SeedAsync(db, clock);
        var bot = new BotService(db, new StudentReferenceStore(clock), clock, new CapturingProvider(), Config(), null, new ConversationStateStore());
        var owner = "mock-user";
        var session = "mock-session";
        var ambiguous = await bot.Chat(new("Cargale a Alex la cuota de noviembre, pagó $13.000 por transferencia."), owner, session, default);
        Assert.Equal("clarification", ambiguous.State);
        Assert.Equal(2, ambiguous.StudentCandidates?.Count);
        var proposed = await bot.Chat(new("El segundo"), owner, session, default);
        Assert.True(proposed.ConfirmationRequired);
        Assert.Equal(13000, proposed.PendingOperation?.After?.Amount);
        Assert.Equal(2, await db.Cuotas.CountAsync());
        var corrected = await bot.Chat(new("Perdón, eran $12.000"), owner, session, default);
        Assert.Equal(12000, corrected.PendingOperation?.After?.Amount);
        Assert.Equal(2, await db.Cuotas.CountAsync());
        var confirmed = await bot.Chat(new("Confirmo"), owner, session, default);
        Assert.Equal("confirmed", confirmed.Operation?.Status);
        Assert.Equal(3, await db.Cuotas.CountAsync());
        Assert.Equal(12000, (await db.Cuotas.OrderByDescending(payment => payment.Id).FirstAsync()).Monto);
        var debt = await bot.Chat(new("¿Qué meses debe Rio?"), owner, session, default);
        Assert.NotNull(debt.Debt);
        var statistics = await bot.Chat(new("Mostrame las estadísticas de este mes"), owner, session, default);
        Assert.NotNull(statistics.Statistics);
        var voidChoice = await bot.Chat(new("Anulá el pago de Alex de noviembre porque duplicado"), owner, session, default);
        Assert.Equal(2, voidChoice.StudentCandidates?.Count);
        var voidProposal = await bot.Chat(new("El segundo"), owner, session, default);
        Assert.Equal("void", voidProposal.PendingOperation?.Kind);
        Assert.Equal("active", (await db.Cuotas.OrderByDescending(payment => payment.Id).FirstAsync()).Estado);
        var voidConfirmed = await bot.Chat(new("Confirmo"), owner, session, default);
        Assert.Equal("confirmed", voidConfirmed.Operation?.Status);
        Assert.Equal("voided", (await db.Cuotas.OrderByDescending(payment => payment.Id).FirstAsync()).Estado);
        var restoreChoice = await bot.Chat(new("Restaurá el pago de Alex de noviembre porque se verificó el pago"), owner, session, default);
        Assert.Equal(2, restoreChoice.StudentCandidates?.Count);
        var restoreProposal = await bot.Chat(new("El segundo"), owner, session, default);
        Assert.Equal("restore", restoreProposal.PendingOperation?.Kind);
        var restoreConfirmed = await bot.Chat(new("Confirmo"), owner, session, default);
        Assert.Equal("confirmed", restoreConfirmed.Operation?.Status);
        Assert.Equal("active", (await db.Cuotas.OrderByDescending(payment => payment.Id).FirstAsync()).Estado);
        var editChoice = await bot.Chat(new("Corregí el pago de Alex de noviembre a $11.000 por error de carga"), owner, session, default);
        Assert.Equal(2, editChoice.StudentCandidates?.Count);
        var editProposal = await bot.Chat(new("El segundo"), owner, session, default);
        Assert.Equal("edit", editProposal.PendingOperation?.Kind);
        Assert.Equal(11000, editProposal.PendingOperation?.After?.Amount);
        Assert.Equal(12000, (await db.Cuotas.OrderByDescending(payment => payment.Id).FirstAsync()).Monto);
        var editConfirmed = await bot.Chat(new("Confirmo"), owner, session, default);
        Assert.Equal("confirmed", editConfirmed.Operation?.Status);
        Assert.Equal(11000, (await db.Cuotas.OrderByDescending(payment => payment.Id).FirstAsync()).Monto);
    }

    [Fact]
    public async Task AnnualRevenueToolLetsModelAnswerWithoutAskingForMonth()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var db = new TaekwondoDbContext(new DbContextOptionsBuilder<TaekwondoDbContext>().UseSqlite(connection).Options);
        await db.Database.MigrateAsync();
        var clock = new FixedClock();
        await LocalDatabase.SeedAsync(db, clock);
        var config = Config();
        var handler = new AnnualRevenueHandler();
        var engine = new OpenAiConversationEngine(new HttpClient(handler), config, new ProviderUsageLedger(config));
        var bot = new BotService(db, new StudentReferenceStore(clock), clock, new OpenAiNamedProvider(), config, engine, new ConversationStateStore());
        var response = await bot.Chat(new("Dame el mes con mas recaudacion del año"), "user-a", "session-a", default);
        Assert.Equal("answer", response.State);
        Assert.Equal("openai", response.Provider);
        Assert.Contains("septiembre", response.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2026", response.Message);
        Assert.Contains("\"month\":9", handler.ToolOutput);
        Assert.Contains("\"totalAmount\":10000", handler.ToolOutput);
    }

    [Fact]
    public async Task OpenAiSearchPreparesVoidButWaitsForUserConfirmation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var db = new TaekwondoDbContext(new DbContextOptionsBuilder<TaekwondoDbContext>().UseSqlite(connection).Options);
        await db.Database.MigrateAsync();
        var clock = new FixedClock();
        await LocalDatabase.SeedAsync(db, clock);
        var config = Config();
        var engine = new OpenAiConversationEngine(new HttpClient(new SingleStudentSearchHandler()), config, new ProviderUsageLedger(config));
        var bot = new BotService(db, new StudentReferenceStore(clock), clock, new OpenAiNamedProvider(), config, engine, new ConversationStateStore());
        var proposal = await bot.Chat(new("Anulá el pago de Sol de noviembre por duplicado"), "user-a", "session-a", default);
        Assert.Equal("void", proposal.PendingOperation?.Kind);
        Assert.True(proposal.ConfirmationRequired);
        Assert.Equal("active", (await db.Cuotas.OrderByDescending(payment => payment.Id).FirstAsync()).Estado);
        var confirmed = await bot.Chat(new("Confirmo"), "user-a", "session-a", default);
        Assert.Equal("confirmed", confirmed.Operation?.Status);
        Assert.Equal("voided", (await db.Cuotas.OrderByDescending(payment => payment.Id).FirstAsync()).Estado);
    }

    private sealed class OpenAiNamedProvider : ITaekwonBotProvider
    {
        public string Name => "openai";
        public Task<ProviderResponse> RespondAsync(ProviderRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class AnnualRevenueHandler : HttpMessageHandler
    {
        private int calls;
        public string ToolOutput { get; private set; } = "";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            calls++;
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            var input = body.RootElement.GetProperty("input");
            ToolOutput = string.Join(" ", input.EnumerateArray().Where(item => item.TryGetProperty("output", out _)).Select(item => item.GetProperty("output").GetString()));
            object output = calls == 1
                ? new[] { new { type = "function_call", name = "get_year_revenue", call_id = "call_year", arguments = "{\"year\":2026}", content = Array.Empty<object>() } }
                : new[] { new { type = "message", name = "", call_id = "", arguments = "", content = new[] { new { type = "output_text", text = "En 2026, septiembre fue el mes con más recaudación." } } } };
            var json = JsonSerializer.Serialize(new { status = "completed", output, usage = new { input_tokens = 100, output_tokens = 10, input_tokens_details = new { cached_tokens = 0 } } });
            return new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class SingleStudentSearchHandler : HttpMessageHandler
    {
        private int calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            calls++;
            object output = calls == 1
                ? new[] { new { type = "function_call", name = "search_students", call_id = "call_student", arguments = "{\"query\":\"Sol\",\"year\":null,\"month\":null,\"amount\":null,\"method\":null}", content = Array.Empty<object>() } }
                : new[] { new { type = "message", name = "", call_id = "", arguments = "", content = new[] { new { type = "output_text", text = "Preparé la operación para revisión." } } } };
            var json = JsonSerializer.Serialize(new { status = "completed", output, usage = new { input_tokens = 100, output_tokens = 10, input_tokens_details = new { cached_tokens = 0 } } });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") });
        }
    }

    private sealed class ForgedPaymentHandler(bool directClaim = false) : HttpMessageHandler
    {
        private int calls;
        public string LastToolOutput { get; private set; } = "";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            calls++;
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            var input = body.RootElement.GetProperty("input");
            LastToolOutput = string.Join(" ", input.EnumerateArray().Where(x => x.TryGetProperty("output", out _)).Select(x => x.GetProperty("output").GetString()));
            object output = directClaim || calls > 2
                ? new[] { new { type = "message", name = "", call_id = "", arguments = "", content = new[] { new { type = "output_text", text = directClaim ? "Ya registré un pago sin confirmación." : "Necesito los datos que faltan." } } } }
                : new[] { new { type = "function_call", name = calls == 1 ? "search_students" : "prepare_payment", call_id = "call_" + calls,
                    arguments = calls == 1 ? "{\"query\":\"Rio\",\"year\":null,\"month\":null,\"amount\":null,\"method\":null}" :
                    "{\"studentRef\":\"" + ExtractRef(LastToolOutput) + "\",\"year\":2026,\"month\":11,\"amount\":13000,\"method\":\"Transferencia\"}", content = Array.Empty<object>() } };
            var json = JsonSerializer.Serialize(new { status = "completed", output, usage = new { input_tokens = 100, output_tokens = 10, input_tokens_details = new { cached_tokens = 0 } } });
            return new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
        private static string ExtractRef(string output)
        {
            using var json = JsonDocument.Parse(output);
            return json.RootElement.GetProperty("students")[0].GetProperty("studentRef").GetString()!;
        }
    }
}
