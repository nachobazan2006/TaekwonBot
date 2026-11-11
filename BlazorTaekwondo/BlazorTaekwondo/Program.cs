using ApexCharts;
using BlazorTaekwondo.Components;
using BlazorTaekwondo.Endpoints;
using BlazorTaekwondo.Security;
using BlazorTaekwondo.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.JSInterop;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);
LocalWorkspaceConfiguration.Configure(builder);
builder.WebHost.UseUrls("http://127.0.0.1:5006");
builder.Services.AddRazorComponents().AddInteractiveServerComponents().AddHubOptions(options =>
{
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
    options.HandshakeTimeout = TimeSpan.FromSeconds(30);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
});
builder.Services.AddApexCharts();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.Cookie.Name = "TaekwondoVillaAllende.Auth";
    options.LoginPath = "/login";
    options.LogoutPath = "/auth/logout";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/local-api")) context.Response.StatusCode = 401;
        else context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Path.StartsWithSegments("/local-api")) context.Response.StatusCode = 403;
        else context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAntiforgery(options => options.HeaderName = "X-TaekwonBot-Antiforgery");
builder.Services.AddSingleton<LocalApiIdentitySigner>();
builder.Services.AddHttpClient("LocalBackend", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"]!);
    client.Timeout = TimeSpan.FromSeconds(25);
}).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false });
builder.Services.AddScoped(sp => new HttpClient(new BrowserBffMessageHandler(sp.GetRequiredService<IJSRuntime>()))
{
    BaseAddress = new Uri("http://local-bff.invalid/"),
    Timeout = TimeSpan.FromSeconds(35)
});
builder.Services.AddScoped<AlumnoService>();
builder.Services.AddScoped<CuotaService>();
builder.Services.AddScoped<ExamenService>();
builder.Services.AddScoped<CampaniaService>();
builder.Services.AddScoped<MensajeService>();
builder.Services.AddScoped<ReportesService>();
builder.Services.AddScoped<TaekwonBotService>();

var app = builder.Build();
var adminPassword = builder.Configuration["AdminPassword"]!;
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Error", createScopeForErrors: true);
app.Use(async (context, next) =>
{
    if (context.Connection.RemoteIpAddress is { } address && !System.Net.IPAddress.IsLoopback(address))
    {
        context.Response.StatusCode = 403;
        return;
    }
    if (!LocalWorkspaceConfiguration.IsLocalHost(context.Request.Host.Host))
    {
        context.Response.StatusCode = 403;
        return;
    }
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "same-origin";
    await next(context);
});
// Conservar la compatibilidad del inicio interactivo con los assets publicados.
var webRoot = app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value;
    if (path is "/_framework/blazor.web.js" or "/_framework/blazor.server.js")
    {
        var file = Path.Combine(webRoot, path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(file))
        {
            context.Response.ContentType = "text/javascript; charset=utf-8";
            context.Response.Headers.CacheControl = "no-cache";
            await context.Response.SendFileAsync(file);
            return;
        }
    }
    await next(context);
});
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapPost("/auth/login", async (HttpContext context, Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery) =>
{
    try { await antiforgery.ValidateRequestAsync(context); }
    catch (Microsoft.AspNetCore.Antiforgery.AntiforgeryValidationException) { return Results.BadRequest(); }
    var form = await context.Request.ReadFormAsync();
    var username = form["username"].ToString().Trim();
    var password = form["password"].ToString();
    var returnUrl = form["returnUrl"].ToString();
    if (!IsValidClubUser(username, password, adminPassword))
    {
        var redirect = "/login?error=1";
        if (IsLocalUrl(returnUrl)) redirect += $"&returnUrl={Uri.EscapeDataString(returnUrl)}";
        return Results.Redirect(redirect);
    }
    var normalizedUser = string.Equals(username, "Luana", StringComparison.OrdinalIgnoreCase) ? "Luana" : "Matias";
    var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, normalizedUser), new Claim("sid", Guid.NewGuid().ToString("N"))], CookieAuthenticationDefaults.AuthenticationScheme);
    await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity),
        new AuthenticationProperties { IsPersistent = false, AllowRefresh = true });
    return Results.LocalRedirect(IsLocalUrl(returnUrl) ? returnUrl : "/");
});
app.MapPost("/auth/logout", async (HttpContext context, Microsoft.AspNetCore.Antiforgery.IAntiforgery antiforgery) =>
{
    try { await antiforgery.ValidateRequestAsync(context); }
    catch (Microsoft.AspNetCore.Antiforgery.AntiforgeryValidationException) { return Results.BadRequest(); }
    await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.LocalRedirect("/login");
});
app.MapLocalApiEndpoints();
app.MapStaticAssets();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();

static bool IsValidClubUser(string username, string password, string expectedPassword) =>
    System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
        System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(password)),
        System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(expectedPassword)))
    && (string.Equals(username, "Matias", StringComparison.OrdinalIgnoreCase) || string.Equals(username, "Luana", StringComparison.OrdinalIgnoreCase));
static bool IsLocalUrl(string? url) => !string.IsNullOrWhiteSpace(url)
    && Uri.TryCreate(url, UriKind.Relative, out _) && url.StartsWith('/') && !url.StartsWith("//") && !url.Contains('\\');
public partial class Program { }

