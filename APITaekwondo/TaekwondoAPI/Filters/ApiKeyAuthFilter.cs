using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TaekwondoAPI.Filters
{
    /// <summary>
    /// Aplicar en controllers/acciones que Twilio u otros externos llaman
    /// sin poder incluir nuestro X-Api-Key (ej: webhooks entrantes).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class SkipApiKeyAuthAttribute : Attribute { }

    /// <summary>
    /// Filtro global que valida el header X-Api-Key contra la variable de entorno ApiKey.
    /// Si ApiKey no está configurada (desarrollo local), permite todo.
    /// </summary>
    public class ApiKeyAuthFilter : IAuthorizationFilter
    {
        private const string HeaderName = "X-Api-Key";
        private readonly string _apiKey;

        public ApiKeyAuthFilter(IConfiguration config)
        {
            _apiKey = config["ApiKey"] ?? string.Empty;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            // Sin clave configurada → entorno local sin restricción
            if (string.IsNullOrWhiteSpace(_apiKey)) return;

            // Endpoints marcados explícitamente (webhooks Twilio, etc.)
            var endpoint = context.HttpContext.GetEndpoint();
            if (endpoint?.Metadata.GetMetadata<SkipApiKeyAuthAttribute>() is not null) return;

            if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var key)
                || key != _apiKey)
            {
                context.Result = new UnauthorizedObjectResult(new { error = "No autorizado." });
            }
        }
    }
}
