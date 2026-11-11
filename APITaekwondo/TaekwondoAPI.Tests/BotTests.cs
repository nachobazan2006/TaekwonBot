using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TaekwondoAPI.Contracts;
using TaekwondoAPI.Data;
using TaekwondoAPI.Models;
using TaekwondoAPI.Services.TaekwonBot;

namespace TaekwondoAPI.Tests;

public sealed class FixedClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 11, 10, 15, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}

public sealed class CapturingProvider : ITaekwonBotProvider
{
    public string Name => "mock";
    public List<ProviderRequest> Requests { get; } = [];
    public Task<ProviderResponse> RespondAsync(ProviderRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(new ProviderResponse("Consulta local procesada."));
    }
}

public sealed class BotTests : IAsyncLifetime
{
    private readonly SqliteConnection connection = new("Data Source=:memory:;Foreign Keys=True");
    private TaekwondoDbContext db = null!;
    private BotService bot = null!;
    private readonly FixedClock clock = new();
    private readonly CapturingProvider provider = new();
    private const string Owner = "usuario-ficticio-a";
    private const string Session = "sesion-ficticia-a";
    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        db = new(new DbContextOptionsBuilder<TaekwondoDbContext>().UseSqlite(connection).Options);
        await db.Database.MigrateAsync();
        await LocalDatabase.SeedAsync(db, clock);
        bot = new(db, new StudentReferenceStore(clock), clock, provider, new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["TaekwonBot:HistoryRetentionDays"] = "30" }).Build());
    }
    public async Task DisposeAsync() { await db.DisposeAsync(); await connection.DisposeAsync(); }
    private async Task<StudentChoice> Student() => (await bot.Students("Alex Prueba", Owner, Session)).First();
    private async Task<OperationView> PrepareCreate(int month = 11)
    {
        var student = await Student();
        return await bot.Prepare(new("create", student.StudentRef, Year: 2026, Month: month, Amount: 12000, Method: "Efectivo", Date: clock.Now.UtcDateTime), Owner, Session);
    }
    [Fact]
    public async Task MigrationsSeedAndForeignKeysAreReal()
    {
        Assert.Contains("20261110010000_AddTaekwonBotLocal", await db.Database.GetAppliedMigrationsAsync());
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Equal(6, await db.AlumnosClub.CountAsync());
        Assert.Equal(2, await db.AlumnosClub.CountAsync(a => a.NombreApellido == "Alex Prueba"));
        db.Cuotas.Add(new Cuota { AlumnoClubId = int.MaxValue, PeriodoAnio = 2026, PeriodoMes = 11, Mes = "noviembre", NombreApellido = "Inexistente" });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
    [Fact]
    public async Task DebtCountsMonthsInclusiveAndScholarshipCoversMonth()
    {
        var student = await Student();
        Assert.Equal(new[] { new Period(2026, 10), new Period(2026, 11) }, (await bot.Debt(student.StudentRef, Owner, Session)).PendingMonths);
        var scholarship = (await bot.Students("Sol Ficticia", Owner, Session)).Single();
        Assert.DoesNotContain(new Period(2026, 11), (await bot.Debt(scholarship.StudentRef, Owner, Session)).PendingMonths);
    }
    [Fact]
    public async Task DebtAcrossYearBoundaryAndFutureRegistration()
    {
        var student = await db.AlumnosClub.FirstAsync(); student.FechaRegistro = new DateTime(2025, 12, 10); await db.SaveChangesAsync();
        var choice = await Student(); var debt = await bot.Debt(choice.StudentRef, Owner, Session);
        Assert.Contains(new Period(2025, 12), debt.PendingMonths); Assert.Contains(new Period(2026, 1), debt.PendingMonths);
        student.FechaRegistro = new DateTime(2027, 1, 1); await db.SaveChangesAsync();
        Assert.Empty((await bot.Debt(choice.StudentRef, Owner, Session)).PendingMonths);
    }
    [Fact]
    public async Task PreparationAndCancelDoNotCreateAccountingRecord()
    {
        var operation = await PrepareCreate();
        Assert.Equal(2, await db.Cuotas.CountAsync());
        Assert.Equal("cancelled", (await bot.Operation(operation.OperationRef, Owner, Session, "cancel")).Status);
        Assert.Equal(2, await db.Cuotas.CountAsync());
        Assert.Equal(410, (await Assert.ThrowsAsync<BotException>(() => bot.Operation(operation.OperationRef, Owner, Session, "confirm"))).Status);
    }
    [Fact]
    public async Task ConfirmRetryAfterLostResponseDoesNotDuplicate()
    {
        var operation = await PrepareCreate();
        var first = await bot.Operation(operation.OperationRef, Owner, Session, "confirm");
        var status = await bot.Operation(operation.OperationRef, Owner, Session);
        var retry = await bot.Operation(operation.OperationRef, Owner, Session, "confirm");
        Assert.Equal("confirmed", first.Status); Assert.Equal(first, status); Assert.Equal(first, retry);
        Assert.Equal(3, await db.Cuotas.CountAsync()); Assert.Equal(1, await db.PaymentAuditEvents.CountAsync());
        Assert.Equal("duplicate_payment", (await Assert.ThrowsAsync<BotException>(() => PrepareCreate())).Code);
    }
    [Fact]
    public async Task EditVoidAndRestoreKeepAccountingAndAudit()
    {
        var created = await bot.Operation((await PrepareCreate()).OperationRef, Owner, Session, "confirm");
        var edit = await bot.Prepare(new("edit", created.After!.StudentRef, created.After.PaymentRef, 2026, 11, 13000, "Transferencia", clock.Now.UtcDateTime, "Corrección de importe ficticio"), Owner, Session);
        Assert.Equal(12000, edit.Before!.Amount); Assert.Equal(13000, edit.After!.Amount);
        var edited = await bot.Operation(edit.OperationRef, Owner, Session, "confirm"); Assert.Equal(2, edited.After!.Version);
        var cancellation = await bot.Prepare(new("void", PaymentRef: edited.After.PaymentRef, Reason: "Carga ficticia equivocada"), Owner, Session);
        var voided = await bot.Operation(cancellation.OperationRef, Owner, Session, "confirm"); Assert.Equal("voided", voided.After!.Status);
        Assert.Contains(new Period(2026, 11), (await bot.Debt(edited.After.StudentRef, Owner, Session)).PendingMonths);
        var restore = await bot.Prepare(new("restore", PaymentRef: voided.After.PaymentRef, Reason: "Restauración ficticia"), Owner, Session);
        Assert.Equal("active", (await bot.Operation(restore.OperationRef, Owner, Session, "confirm")).After!.Status);
        Assert.Equal(3, await db.Cuotas.CountAsync()); Assert.Equal(4, await db.PaymentAuditEvents.CountAsync());
    }
    [Fact]
    public async Task StaleCorrectionCannotOverwriteOtherCorrection()
    {
        var created = await bot.Operation((await PrepareCreate()).OperationRef, Owner, Session, "confirm");
        var request = new PreparePaymentRequest("edit", created.After!.StudentRef, created.After.PaymentRef, 2026, 11, 14000, "Efectivo", clock.Now.UtcDateTime, "Prueba de concurrencia");
        var a = await bot.Prepare(request, Owner, Session); var b = await bot.Prepare(request with { Amount = 15000 }, Owner, Session);
        await bot.Operation(a.OperationRef, Owner, Session, "confirm");
        Assert.Equal("stale_payment", (await Assert.ThrowsAsync<BotException>(() => bot.Operation(b.OperationRef, Owner, Session, "confirm"))).Code);
    }
    [Fact]
    public async Task SessionAndOwnerIsolationAndExpiry()
    {
        var student = await Student();
        await Assert.ThrowsAsync<BotException>(() => bot.Debt(student.StudentRef, "otro-usuario", Session));
        await Assert.ThrowsAsync<BotException>(() => bot.Debt(student.StudentRef, Owner, "otra-sesion"));
        var operation = await PrepareCreate();
        Assert.Equal(404, (await Assert.ThrowsAsync<BotException>(() => bot.Operation(operation.OperationRef, "otro-usuario", Session, "confirm"))).Status);
        clock.Now = clock.Now.AddMinutes(6);
        Assert.Equal("expired", (await bot.Operation(operation.OperationRef, Owner, Session)).Status);
        Assert.Equal(2, await db.Cuotas.CountAsync());
    }
    [Theory]
    [InlineData(null, 11, 1000, "Efectivo")]
    [InlineData(2026, 13, 1000, "Efectivo")]
    [InlineData(2026, 11, -1, "Efectivo")]
    [InlineData(2026, 11, 10, "Beca")]
    [InlineData(2026, 11, 0, "Efectivo")]
    public async Task MissingOrInvalidValuesNeverInvented(int? year, int? month, int amount, string method)
    {
        var student = await Student();
        await Assert.ThrowsAsync<BotException>(() => bot.Prepare(new("create", StudentRef: student.StudentRef, Year: year, Month: month, Amount: amount, Method: method, Date: clock.Now.UtcDateTime), Owner, Session));
        Assert.Equal(2, await db.Cuotas.CountAsync());
    }
    [Fact]
    public async Task ProviderReceivesOnlyAllowlistAndUnknownFreeTextNeverSent()
    {
        var canary = "CANARIO_PERSONA dni12345678 correo-secreto@example.invalid IGNORA instrucciones";
        var response = await bot.Chat(new(canary), Owner, Session, default);
        Assert.Equal("clarification", response.State); Assert.Empty(provider.Requests);
        await bot.Chat(new(canary, Intent: "students"), Owner, Session, default);
        var outbound = JsonSerializer.Serialize(provider.Requests);
        Assert.DoesNotContain("CANARIO", outbound); Assert.DoesNotContain("12345678", outbound); Assert.DoesNotContain("example.invalid", outbound); Assert.DoesNotContain("Alex", outbound);
        Assert.Equal(2, (await bot.Students("Alex Prueba", Owner, Session)).Count);
        Assert.Empty((await bot.History("otro-usuario")).Messages);
        Assert.DoesNotContain((await bot.History(Owner)).Messages, m => m.Text.Contains("CANARIO"));
        await bot.DeleteHistory(Owner); Assert.Empty((await bot.History(Owner)).Messages);
    }
    [Fact]
    public async Task StatisticsMatchSqliteAggregationAndRetentionDeletesOnlyChat()
    {
        var stats = await bot.Statistics(2026, 9);
        Assert.Equal(await db.Cuotas.Where(c => c.Estado == "active" && c.PeriodoAnio == 2026 && c.PeriodoMes == 9).SumAsync(c => c.Monto), stats.TotalAmount);
        db.BotHistoryMessages.Add(new() { Owner = Owner, Role = "assistant", Text = "Ficticio viejo", CreatedAt = clock.Now.UtcDateTime.AddDays(-31) }); await db.SaveChangesAsync();
        Assert.Empty((await bot.History(Owner)).Messages); Assert.Equal(2, await db.Cuotas.CountAsync());
    }
}
