using Microsoft.EntityFrameworkCore;
using TaekwondoAPI.Data;

namespace TaekwondoAPI.Services.TaekwonBot;

public sealed class HistoryRetentionService(IServiceScopeFactory scopes, IConfiguration config, TimeProvider clock) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(10));
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<TaekwondoDbContext>();
            var cutoff = clock.GetUtcNow().UtcDateTime.AddDays(-Math.Clamp(config.GetValue("TaekwonBot:HistoryRetentionDays", 30), 1, 3650));
            try { await db.BotHistoryMessages.Where(m => m.CreatedAt < cutoff).ExecuteDeleteAsync(stoppingToken); }
            catch (Microsoft.Data.Sqlite.SqliteException exception) when (exception.SqliteErrorCode is 5 or 6) { /* Se reintenta en el próximo ciclo sin interrumpir operaciones financieras. */ }
            if (!await timer.WaitForNextTickAsync(stoppingToken)) break;
        }
    }
}
