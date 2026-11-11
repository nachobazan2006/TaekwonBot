using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TaekwondoAPI.Services.TaekwonBot;

public sealed class ProviderUsageLedger
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly string path;
    private readonly decimal dailyBudget;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public const decimal ReservationUsd = .05m;

    public ProviderUsageLedger(IConfiguration configuration)
    {
        var root = configuration["TaekwonBot:WorkspaceRoot"] ?? throw new InvalidOperationException("Falta workspace local.");
        path = Path.Combine(Path.GetFullPath(root), ".local", "data", "provider-usage.json");
        dailyBudget = configuration.GetValue<decimal?>("TaekwonBot:OpenAi:DailyBudgetUsd") ?? 1m;
        if (dailyBudget <= 0 || dailyBudget > 100m) throw new InvalidOperationException("El presupuesto diario debe ser explícito y estar entre USD0 y USD100.");
    }

    public async Task<string> ReserveAsync(string owner, CancellationToken cancellationToken)
    {
        var ownerHash = OwnerHash(owner);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var rows = await ReadAsync(cancellationToken);
            var day = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var committed = rows.Where(row => row.OwnerHash == ownerHash && row.Day == day)
                .Sum(row => row.KnownCostUsd + row.HeldUsd);
            if (committed + ReservationUsd > dailyBudget)
                throw new OpenAiProviderException("OPENAI_DAILY_BUDGET", "Se alcanzó el presupuesto diario de OpenAI de este usuario. Las consultas locales siguen disponibles.");
            var id = Guid.NewGuid().ToString("N");
            rows.Add(new UsageEntry { Id = id, OwnerHash = ownerHash, Day = day, HeldUsd = ReservationUsd, State = "pending" });
            await WriteAsync(rows, cancellationToken);
            return id;
        }
        finally { gate.Release(); }
    }

    public async Task CompleteAsync(string id, ProviderUsage? usage, bool uncertainCharge, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            var rows = await ReadAsync(cancellationToken);
            var row = rows.SingleOrDefault(item => item.Id == id) ?? throw new InvalidOperationException("Reserva de consumo inexistente.");
            if (row.State != "pending") return;
            row.State = uncertainCharge ? "unknown" : usage is null ? "released" : "completed";
            row.Model = usage?.Model;
            row.InputTokens = usage?.InputTokens ?? 0;
            row.CachedInputTokens = usage?.CachedInputTokens ?? 0;
            row.OutputTokens = usage?.OutputTokens ?? 0;
            row.KnownCostUsd = usage?.CostUsd ?? 0;
            row.HeldUsd = uncertainCharge ? Math.Max(0, ReservationUsd - row.KnownCostUsd) : 0;
            await WriteAsync(rows, cancellationToken);
        }
        finally { gate.Release(); }
    }

    public async Task<UsageSnapshot> SnapshotAsync(string owner, CancellationToken cancellationToken)
    {
        var hash = OwnerHash(owner);
        await gate.WaitAsync(cancellationToken);
        try
        {
            var day = DateTime.UtcNow.ToString("yyyy-MM-dd");
            var rows = (await ReadAsync(cancellationToken)).Where(row => row.OwnerHash == hash && row.Day == day).ToArray();
            var known = rows.Sum(row => row.KnownCostUsd);
            var held = rows.Sum(row => row.HeldUsd);
            return new(day, dailyBudget, known, held, Math.Max(0, dailyBudget - known - held), rows.Length,
                rows.Sum(row => row.InputTokens), rows.Sum(row => row.CachedInputTokens), rows.Sum(row => row.OutputTokens));
        }
        finally { gate.Release(); }
    }

    private async Task<List<UsageEntry>> ReadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<UsageEntry>>(await File.ReadAllTextAsync(path, cancellationToken), Json)
                ?? throw new JsonException();
        }
        catch (JsonException) { throw new InvalidOperationException("El registro de consumo necesita revisión; no se emitirán nuevas solicitudes."); }
    }

    private async Task WriteAsync(List<UsageEntry> rows, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        await File.WriteAllTextAsync(temp, JsonSerializer.Serialize(rows, Json), cancellationToken);
        File.Move(temp, path, overwrite: true);
    }

    private static string OwnerHash(string owner)
    {
        if (string.IsNullOrWhiteSpace(owner)) throw new UnauthorizedAccessException();
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(owner))).ToLowerInvariant();
    }

    public sealed class UsageEntry
    {
        public string Id { get; set; } = "";
        public string OwnerHash { get; set; } = "";
        public string Day { get; set; } = "";
        public string State { get; set; } = "pending";
        public string? Model { get; set; }
        public decimal KnownCostUsd { get; set; }
        public decimal HeldUsd { get; set; }
        public int InputTokens { get; set; }
        public int CachedInputTokens { get; set; }
        public int OutputTokens { get; set; }
    }
}

public sealed record UsageSnapshot(string Day, decimal DailyBudgetUsd, decimal KnownCostUsd,
    decimal ReservedOrUnknownUsd, decimal RemainingUsd, int Attempts, int InputTokens, int CachedInputTokens, int OutputTokens);

public sealed class BudgetedOpenAiProvider(OpenAiTaekwonBotProvider provider, ProviderUsageLedger ledger,
    IHttpContextAccessor contextAccessor) : ITaekwonBotProvider
{
    public string Name => "openai";
    public async Task<ProviderResponse> RespondAsync(ProviderRequest request, CancellationToken cancellationToken)
    {
        var context = contextAccessor.HttpContext;
        if (context?.User.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(context.User.Identity.Name))
            throw new UnauthorizedAccessException();
        var reservation = await ledger.ReserveAsync(context.User.Identity.Name, cancellationToken);
        try
        {
            var result = await provider.RespondAsync(request, cancellationToken);
            if (result.Usage is null) throw new OpenAiProviderException("OPENAI_USAGE_MISSING", "No se pudo verificar el consumo del proveedor.");
            await ledger.CompleteAsync(reservation, result.Usage, false, CancellationToken.None);
            return result;
        }
        catch (OpenAiProviderException error)
        {
            var notSent = error.Code is "OPENAI_NOT_CONFIGURED" or "OPENAI_MODEL_NOT_VERIFIED" or "UNSAFE_PROVIDER_CONTEXT";
            await ledger.CompleteAsync(reservation, error.Usage, !notSent, CancellationToken.None);
            throw;
        }
        catch
        {
            // Ante timeout/crash no asumir costo cero: conservar la reserva conservadora.
            await ledger.CompleteAsync(reservation, null, true, CancellationToken.None);
            throw;
        }
    }
}
