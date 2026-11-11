using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using TaekwondoAPI.Authentication;
using TaekwondoAPI.Contracts;
using TaekwondoAPI.Data;
using TaekwondoAPI.Services;
using TaekwondoAPI.Services.TaekwonBot;

var builder = WebApplication.CreateBuilder(args);
var workspace = LocalDatabase.FindWorkspace(builder.Environment.ContentRootPath);
builder.Configuration.AddJsonFile(Path.Combine(workspace, ".local", "settings.json"), optional: false, reloadOnChange: false);
if (!builder.Configuration.GetValue<bool>("TaekwonBot:LocalOnly")) throw new InvalidOperationException("Esta copia solo admite el entorno local ficticio.");
if (Path.GetFullPath(builder.Configuration["TaekwonBot:WorkspaceRoot"] ?? "") != Path.GetFullPath(workspace)) throw new InvalidOperationException("Workspace local inconsistente.");
var signingBytes = Convert.FromBase64String(builder.Configuration["TaekwonBot:SigningKey"] ?? "");
if (signingBytes.Length < 64 || string.IsNullOrEmpty(builder.Configuration["ApiKey"])) throw new InvalidOperationException("Falta la configuración privada local.");
var urls = builder.Configuration["urls"] ?? "http://127.0.0.1:5087";
if (urls.Split(';').Any(url => !Uri.TryCreate(url, UriKind.Absolute, out var uri) || !uri.IsLoopback || uri.Scheme is not ("http" or "https"))) throw new InvalidOperationException("La API solo puede escuchar en loopback.");
builder.WebHost.UseUrls(urls).ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 64 * 1024);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore.Authentication", LogLevel.Error);
builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Warning);
var databasePath = Path.Combine(workspace, ".local", "data", "taekwonbot.db");
Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
builder.Services.AddDbContext<TaekwondoDbContext>(options => options.UseSqlite(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder { DataSource = databasePath, ForeignKeys = true, DefaultTimeout = 10 }.ToString()));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<NonceCache>();
builder.Services.AddSingleton<StudentReferenceStore>();
builder.Services.AddSingleton<ConversationStateStore>();
builder.Services.AddScoped<BotService>();
builder.Services.AddSingleton<MockTaekwonBotProvider>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<ProviderUsageLedger>();
builder.Services.AddHttpClient<OpenAiTaekwonBotProvider>().ConfigurePrimaryHttpMessageHandler(() =>
    new SocketsHttpHandler { AllowAutoRedirect = false, ConnectTimeout = TimeSpan.FromSeconds(10) });
builder.Services.AddHttpClient<OpenAiConversationEngine>().ConfigurePrimaryHttpMessageHandler(() =>
    new SocketsHttpHandler { AllowAutoRedirect = false, ConnectTimeout = TimeSpan.FromSeconds(10) });
builder.Services.AddScoped<BudgetedOpenAiProvider>();
builder.Services.AddScoped<ITaekwonBotProvider>(services =>
    builder.Configuration["TaekwonBot:Provider"] switch
    {
        "mock" => services.GetRequiredService<MockTaekwonBotProvider>(),
        "openai" => services.GetRequiredService<BudgetedOpenAiProvider>(),
        _ => throw new InvalidOperationException("Proveedor no autorizado; elegir mock u openai explícitamente.")
    });
builder.Services.AddHostedService<HistoryRetentionService>();
builder.Services.AddAuthentication(FrontendIdentityAuthenticationHandler.SchemeName).AddScheme<AuthenticationSchemeOptions, FrontendIdentityAuthenticationHandler>(FrontendIdentityAuthenticationHandler.SchemeName, _ => { });
builder.Services.AddAuthorization(options => options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
builder.Services.AddControllers(options => options.Filters.Add(new AuthorizeFilter()));
builder.Services.AddScoped<TwilioWhatsAppService>();
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.AddScoped<IEmailService, SmtpEmailService>();
builder.Services.AddScoped<IReciboPdfService, ReciboPdfService>();

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TaekwondoDbContext>();
    await db.Database.MigrateAsync();
    await LocalDatabase.SeedAsync(db, TimeProvider.System);
}
app.Use(async (context, next) =>
{
    try { await next(); }
    catch (BotException exception)
    {
        context.Response.StatusCode = exception.Status;
        await context.Response.WriteAsJsonAsync(new BotError(exception.Code, exception.Message));
    }
    catch (OpenAiProviderException exception)
    {
        context.Response.StatusCode = exception.Code == "OPENAI_DAILY_BUDGET" ? 429 : 503;
        await context.Response.WriteAsJsonAsync(new BotError(exception.Code, exception.Message));
    }
    catch (Exception exception) when (exception is not OperationCanceledException)
    {
        context.Response.StatusCode = 500;
        await context.Response.WriteAsJsonAsync(new BotError("internal_error", "No se pudo completar la solicitud. Consultá el estado antes de reintentar una operación."));
    }
});
app.UseAuthentication();
app.UseAuthorization();
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";
    if (path.StartsWith("/api/notificaciones", StringComparison.OrdinalIgnoreCase) || path.StartsWith("/api/whatsapp", StringComparison.OrdinalIgnoreCase) || path.EndsWith("/enviar-recibo", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = 409;
        await context.Response.WriteAsJsonAsync(new BotError("local_external_disabled", "Los envíos externos están deshabilitados en el entorno ficticio."));
        return;
    }
    await next();
});
app.MapGet("/health", () => Results.Ok(new { status = "ok", mode = "local", provider = builder.Configuration["TaekwonBot:Provider"] })).AllowAnonymous();
app.MapGet("/api/taekwonbot/usage", async (HttpContext context, ProviderUsageLedger ledger, CancellationToken cancellationToken) =>
    Results.Ok(await ledger.SnapshotAsync(context.User.Identity!.Name!, cancellationToken))).RequireAuthorization();
app.MapControllers();
app.Run();

public partial class Program;
