using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace TaekwondoAPI.Authentication;

public sealed class NonceCache(TimeProvider clock)
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> seen = new();
    public bool Accept(string nonce, DateTimeOffset expires)
    {
        var now = clock.GetUtcNow();
        foreach (var entry in seen.Where(p => p.Value < now)) seen.TryRemove(entry.Key, out _);
        return seen.Count < 100_000 && seen.TryAdd(nonce, expires);
    }
}

public sealed class FrontendIdentityAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder,
    IConfiguration config, NonceCache nonces, TimeProvider clock)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "FrontendIdentity";
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.Authorization.ToString().StartsWith("Bearer ", StringComparison.Ordinal))
            return AuthenticateResult.NoResult();
        try
        {
            var apiKey = config["ApiKey"];
            if (string.IsNullOrEmpty(apiKey) || Request.Headers["X-Api-Key"].ToString() != apiKey)
                return AuthenticateResult.Fail("Credencial local inválida.");
            var signingKey = Convert.FromBase64String(config["TaekwonBot:SigningKey"] ?? "");
            if (signingKey.Length < 64) return AuthenticateResult.Fail("Configuración inválida.");
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false, MaximumTokenSizeInBytes = 8192 };
            var principal = handler.ValidateToken(Request.Headers.Authorization.ToString()[7..], new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(signingKey),
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256], ValidateIssuer = true,
                ValidIssuer = "TaekwonBot.Frontend.Local", ValidateAudience = true, ValidAudience = "TaekwonBot.Api.Local",
                ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true,
                ClockSkew = TimeSpan.FromSeconds(5), NameClaimType = "sub"
            }, out var validated);
            var jwt = (JwtSecurityToken)validated;
            var now = clock.GetUtcNow();
            var subject = principal.FindFirstValue("sub");
            var nonce = principal.FindFirstValue("jti");
            var session = principal.FindFirstValue("sid");
            if (string.IsNullOrWhiteSpace(subject) || subject.Length > 200 || string.IsNullOrWhiteSpace(nonce) || nonce.Length > 128 || string.IsNullOrWhiteSpace(session) || session.Length > 128
                || jwt.ValidTo > now.UtcDateTime.AddSeconds(65) || jwt.ValidFrom > now.UtcDateTime.AddSeconds(5)
                || principal.FindFirstValue("htm") != Request.Method.ToUpperInvariant()
                || principal.FindFirstValue("htu") != Request.PathBase + Request.Path + Request.QueryString)
                return AuthenticateResult.Fail("Solicitud firmada inválida.");
            Request.EnableBuffering(bufferThreshold: 64 * 1024, bufferLimit: 64 * 1024);
            var bodyHash = Convert.ToHexString(await SHA256.HashDataAsync(Request.Body, Context.RequestAborted)).ToLowerInvariant();
            Request.Body.Position = 0;
            if (principal.FindFirstValue("bhash") != bodyHash || !nonces.Accept(nonce, new DateTimeOffset(jwt.ValidTo, TimeSpan.Zero).AddSeconds(5)))
                return AuthenticateResult.Fail("Solicitud alterada o repetida.");
            var identity = new ClaimsIdentity(principal.Claims, SchemeName, "sub", ClaimTypes.Role);
            return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException or FormatException or IOException)
        {
            return AuthenticateResult.Fail("Credencial inválida o vencida.");
        }
    }
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 401;
        return Response.WriteAsJsonAsync(new { code = "session_required", message = "Iniciá sesión para usar esta función." });
    }
    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 403;
        return Response.WriteAsJsonAsync(new { code = "forbidden", message = "No se permite esta solicitud." });
    }
}
