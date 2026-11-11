using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace BlazorTaekwondo.Security;

public sealed class LocalApiIdentitySigner(IConfiguration configuration)
{
    private readonly SigningCredentials credentials = new(
        new SymmetricSecurityKey(Convert.FromBase64String(configuration["TaekwonBot:SigningKey"]!)), SecurityAlgorithms.HmacSha256);
    public string Sign(ClaimsPrincipal user, string method, string pathAndQuery, byte[] body)
    {
        if (user.Identity?.IsAuthenticated != true || string.IsNullOrWhiteSpace(user.Identity.Name) || string.IsNullOrWhiteSpace(user.FindFirstValue("sid")))
            throw new InvalidOperationException("Se requiere una sesión autenticada.");
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken("TaekwonBot.Frontend.Local", "TaekwonBot.Api.Local",
            [new(JwtRegisteredClaimNames.Sub, user.Identity.Name), new("sid", user.FindFirstValue("sid")!), new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
             new("htm", method.ToUpperInvariant()), new("htu", pathAndQuery),
             new("bhash", Convert.ToHexString(SHA256.HashData(body)).ToLowerInvariant())],
            notBefore: now.AddSeconds(-1), expires: now.AddSeconds(59), signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
