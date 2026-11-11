using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using TaekwondoAPI.Authentication;
using TaekwondoAPI.Contracts;
using TaekwondoAPI.Controllers;
using TaekwondoAPI.Data;
using TaekwondoAPI.Services.TaekwonBot;

namespace TaekwondoAPI.Tests;

public sealed class AuthenticationHttpTests : IAsyncLifetime
{
    private readonly byte[] key = RandomNumberGenerator.GetBytes(64);
    private readonly string apiKey = Guid.NewGuid().ToString("N");
    private readonly SqliteConnection connection = new("Data Source=:memory:;Foreign Keys=True");
    private WebApplication app = null!;
    private HttpClient client = null!;
    public async Task InitializeAsync()
    {
        await connection.OpenAsync();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["ApiKey"] = apiKey, ["TaekwonBot:SigningKey"] = Convert.ToBase64String(key) });
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<NonceCache>(); builder.Services.AddSingleton<StudentReferenceStore>();
        builder.Services.AddSingleton<ITaekwonBotProvider, MockTaekwonBotProvider>(); builder.Services.AddScoped<BotService>();
        builder.Services.AddDbContext<TaekwondoDbContext>(options => options.UseSqlite(connection));
        builder.Services.AddAuthentication(FrontendIdentityAuthenticationHandler.SchemeName).AddScheme<AuthenticationSchemeOptions, FrontendIdentityAuthenticationHandler>(FrontendIdentityAuthenticationHandler.SchemeName, _ => { });
        builder.Services.AddAuthorization(options => options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        builder.Services.AddControllers(options => options.Filters.Add(new AuthorizeFilter())).AddApplicationPart(typeof(TaekwonBotController).Assembly);
        app = builder.Build();
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<TaekwondoDbContext>(); await db.Database.MigrateAsync(); await LocalDatabase.SeedAsync(db, TimeProvider.System);
        }
        app.Use(async (context, next) =>
        {
            try { await next(); }
            catch (BotException error) { context.Response.StatusCode = error.Status; await Microsoft.AspNetCore.Http.HttpResponseJsonExtensions.WriteAsJsonAsync(context.Response, new BotError(error.Code, error.Message)); }
        });
        app.UseAuthentication(); app.UseAuthorization();
        app.MapGet("/health", () => new { status = "ok" }).AllowAnonymous(); app.MapControllers();
        await app.StartAsync();
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        client = new HttpClient { BaseAddress = new Uri(address) };
    }
    public async Task DisposeAsync() { client.Dispose(); await app.StopAsync(); await app.DisposeAsync(); await connection.DisposeAsync(); }
    private string Token(string method, string path, string body = "", string owner = "ficticio-a", string session = "sesion-a", string? hashOverride = null, string? audience = null, int expirySeconds = 59, bool includeSession = true)
    {
        var claims = new List<Claim> { new("sub", owner), new("jti", Guid.NewGuid().ToString("N")), new("htm", method), new("htu", path), new("bhash", hashOverride ?? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body))).ToLowerInvariant()) };
        if (includeSession) claims.Add(new("sid", session));
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken("TaekwonBot.Frontend.Local", audience ?? "TaekwonBot.Api.Local", claims, now.AddMinutes(-1), now.AddSeconds(expirySeconds), new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
    private HttpRequestMessage Request(string method, string path, string body = "", string owner = "ficticio-a", string session = "sesion-a", string? token = null)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (body.Length > 0) request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        request.Headers.Add("X-Api-Key", apiKey); request.Headers.Authorization = new("Bearer", token ?? Token(method, path, body, owner, session));
        return request;
    }
    [Theory]
    [InlineData("GET", "/api/alumnosclub")]
    [InlineData("GET", "/api/alumnos")]
    [InlineData("GET", "/api/cuotas")]
    [InlineData("GET", "/api/examenes")]
    [InlineData("GET", "/api/configuracionmensajes")]
    [InlineData("POST", "/api/notificaciones/enviar-pendientes")]
    [InlineData("POST", "/api/whatsapp/inbound")]
    [InlineData("GET", "/api/taekwonbot/history")]
    [InlineData("GET", "/api/taekwonbot/students")]
    [InlineData("POST", "/api/taekwonbot/payments/prepare")]
    public async Task EverySensitiveRouteRejectsApiKeyWithoutSession(string method, string path)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path); request.Headers.Add("X-Api-Key", apiKey);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(request)).StatusCode);
    }
    [Fact]
    public async Task SignedSessionWorksAndNonceReplayIsRejected()
    {
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        var token = Token("GET", "/api/taekwonbot/students");
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Request("GET", "/api/taekwonbot/students", token: token))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(Request("GET", "/api/taekwonbot/students", token: token))).StatusCode);
    }
    [Fact]
    public async Task SignatureAudienceExpirySessionMethodPathAndBodyAreValidated()
    {
        const string path = "/api/taekwonbot/students";
        var good = Token("GET", path);
        var badSignature = good[..(good.LastIndexOf('.') + 1)] + new string('A', good.Length - good.LastIndexOf('.') - 1);
        var invalidTokens = new[] { badSignature, Token("GET", path, audience: "otro"), Token("GET", path, expirySeconds: -20), Token("GET", path, includeSession: false), Token("POST", path), Token("GET", "/api/cuotas"), Token("GET", path, hashOverride: "00") };
        foreach (var invalid in invalidTokens) Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(Request("GET", path, token: invalid))).StatusCode);
        var raw = "{\"text\":\"ayuda\"}";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(Request("POST", "/api/taekwonbot/chat", "{\"text\":\"cambiado\"}", token: Token("POST", "/api/taekwonbot/chat", raw)))).StatusCode);
    }
    [Fact]
    public async Task BothUsersHaveEqualAccessAndHistoryRemainsIsolated()
    {
        foreach (var owner in new[] { "ficticio-a", "ficticio-b" }) Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Request("GET", "/api/cuotas", owner: owner))).StatusCode);
        var post = await client.SendAsync(Request("POST", "/api/taekwonbot/chat", "{\"text\":\"ayuda\"}")); Assert.Equal(HttpStatusCode.OK, post.StatusCode);
        var a = await (await client.SendAsync(Request("GET", "/api/taekwonbot/history"))).Content.ReadFromJsonAsync<HistoryPage>();
        var b = await (await client.SendAsync(Request("GET", "/api/taekwonbot/history", owner: "ficticio-b"))).Content.ReadFromJsonAsync<HistoryPage>();
        Assert.Equal(2, a!.Messages.Count); Assert.Empty(b!.Messages);
    }
    [Fact]
    public async Task HttpConcurrentConfirmAndLostResponseProduceOneAccountingWrite()
    {
        var students = await (await client.SendAsync(Request("GET", "/api/taekwonbot/students"))).Content.ReadFromJsonAsync<StudentChoice[]>();
        var student = students!.First();
        var today = DateTime.UtcNow;
        var body = System.Text.Json.JsonSerializer.Serialize(new PreparePaymentRequest("create", student.StudentRef, Year: today.Year, Month: today.Month, Amount: 12300, Method: "Efectivo", Date: today), new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        var preparedResponse = await client.SendAsync(Request("POST", "/api/taekwonbot/payments/prepare", body)); Assert.Equal(HttpStatusCode.OK, preparedResponse.StatusCode);
        var operation = (await preparedResponse.Content.ReadFromJsonAsync<OperationView>())!;
        var route = $"/api/taekwonbot/operations/{operation.OperationRef}/confirm";
        var results = await Task.WhenAll(client.SendAsync(Request("POST", route)), client.SendAsync(Request("POST", route)));
        Assert.All(results, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        // Se descartan las respuestas, simulando que la UI perdió el resultado tras el commit.
        foreach (var response in results) response.Dispose();
        var status = await (await client.SendAsync(Request("GET", $"/api/taekwonbot/operations/{operation.OperationRef}"))).Content.ReadFromJsonAsync<OperationView>();
        Assert.Equal("confirmed", status!.Status);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Request("POST", route))).StatusCode);
        using var scope = app.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<TaekwondoDbContext>();
        Assert.Equal(3, await db.Cuotas.CountAsync()); Assert.Equal(1, await db.PaymentAuditEvents.CountAsync());
        Assert.Equal(HttpStatusCode.Conflict, (await client.SendAsync(Request("DELETE", "/api/cuotas/1"))).StatusCode);
    }
}
