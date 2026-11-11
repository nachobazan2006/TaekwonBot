using BlazorTaekwondo.Security;
using Microsoft.AspNetCore.Antiforgery;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace BlazorTaekwondo.Endpoints;

public static partial class LocalApiEndpoints
{
    public static void MapLocalApiEndpoints(this WebApplication app)
    {
        app.MapGet("/local-api/session", (HttpContext context, IAntiforgery antiforgery) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (!context.User.HasClaim(c => c.Type == "sid")) return Results.Unauthorized();
            return Results.Json(new { requestToken = antiforgery.GetAndStoreTokens(context).RequestToken });
        }).RequireAuthorization();
        app.MapMethods("/local-api/{**path}", ["GET", "POST", "PUT", "DELETE"], async (
            string? path, HttpContext context, IAntiforgery antiforgery, IHttpClientFactory factory,
            LocalApiIdentitySigner signer, IConfiguration configuration) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            if (!context.User.HasClaim(c => c.Type == "sid")) return Results.Unauthorized();
            if (!Allowed(context.Request.Method, path))
                return Results.Json(new { code = "ROUTE_NOT_ALLOWED", message = "La operación solicitada no está habilitada." }, statusCode: 404);
            if (context.Request.Method is not "GET")
            {
                try { await antiforgery.ValidateRequestAsync(context); }
                catch (AntiforgeryValidationException)
                { return Results.Json(new { code = "INVALID_REQUEST", message = "La solicitud venció. Recargá la página antes de continuar." }, statusCode: 400); }
            }
            if (context.Request.ContentLength > 64 * 1024)
                return Results.Json(new { code = "REQUEST_TOO_LARGE", message = "La solicitud supera el tamaño permitido." }, statusCode: 413);
            byte[] body;
            using (var buffer = new MemoryStream())
            {
                var bytes = new byte[8192];
                int read;
                while ((read = await context.Request.Body.ReadAsync(bytes)) > 0)
                {
                    if (buffer.Length + read > 64 * 1024)
                        return Results.Json(new { code = "REQUEST_TOO_LARGE", message = "La solicitud supera el tamaño permitido." }, statusCode: 413);
                    buffer.Write(bytes, 0, read);
                }
                body = buffer.ToArray();
            }
            var client = factory.CreateClient("LocalBackend");
            var target = new Uri(client.BaseAddress!, path + context.Request.QueryString);
            using var outgoing = new HttpRequestMessage(new HttpMethod(context.Request.Method), target);
            outgoing.Headers.Authorization = new AuthenticationHeaderValue("Bearer", signer.Sign(context.User, context.Request.Method, target.PathAndQuery, body));
            outgoing.Headers.Add("X-Api-Key", configuration["ApiKey"]);
            if (body.Length > 0)
            {
                outgoing.Content = new ByteArrayContent(body);
                outgoing.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
            }
            try
            {
                // Una desconexión no cancela una escritura: el cliente consultará su estado.
                using var response = await client.SendAsync(outgoing, CancellationToken.None);
                var content = await response.Content.ReadAsByteArrayAsync();
                if ((int)response.StatusCode is >= 300 and < 400)
                    return Results.Json(new { code = "UNEXPECTED_REDIRECT", message = "La API local devolvió una redirección no permitida." }, statusCode: 502);
                context.Response.StatusCode = (int)response.StatusCode;
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.Body.WriteAsync(content);
                return Results.Empty;
            }
            catch (OperationCanceledException)
            { return Results.Json(new { code = "STATUS_UNKNOWN", message = "La API no respondió a tiempo. Consultá el estado de la operación." }, statusCode: 504); }
            catch (HttpRequestException)
            { return Results.Json(new { code = "API_UNAVAILABLE", message = "La API local no está disponible. Comprobá que esté iniciada." }, statusCode: 503); }
        }).RequireAuthorization();
    }
    internal static bool Allowed(string method, string? path)
    {
        if (path is null || path.Contains('\\') || path.Contains('%') || path.Contains("..") || path.Contains("//")) return false;
        if (method == "GET" && path is "health" or "api/taekwonbot/students" or "api/taekwonbot/history" or "api/taekwonbot/debts" or "api/taekwonbot/payments" or "api/taekwonbot/statistics" or "api/taekwonbot/usage") return true;
        if (method == "DELETE" && path == "api/taekwonbot/history") return true;
        if (method == "POST" && path is "api/taekwonbot/chat" or "api/taekwonbot/payments/prepare") return true;
        if (OperationRoute().IsMatch(path)) return method == "GET" && !path.EndsWith("/confirm") && !path.EndsWith("/cancel")
            || method == "POST" && (path.EndsWith("/confirm") || path.EndsWith("/cancel"));
        if (method == "GET" && LegacyRoute().IsMatch(path)) return true;
        if (path is "api/alumnosclub" or "api/cuotas" or "api/examenes" or "api/alumnos") return method is "GET" or "POST";
        if (OriginalItemRoute().IsMatch(path)) return method is "PUT" or "DELETE";
        if (path == "api/configuracionmensajes") return method is "GET" or "PUT";
        if (path == "api/notificaciones/enviar-pendientes") return method == "POST";
        return method == "POST" && ReceiptRoute().IsMatch(path);
    }
    [GeneratedRegex("^api/taekwonbot/operations/[A-Za-z0-9_-]{16,128}(?:/(?:confirm|cancel))?$")]
    private static partial Regex OperationRoute();
    [GeneratedRegex("^api/taekwonbot/legacy/(?:student|payment)/[0-9]+$")]
    private static partial Regex LegacyRoute();
    [GeneratedRegex("^api/(?:alumnosclub|cuotas|examenes|alumnos)/[A-Za-z0-9+_-]{1,80}$")]
    private static partial Regex OriginalItemRoute();
    [GeneratedRegex("^api/cuotas/[0-9]+/enviar-recibo$")]
    private static partial Regex ReceiptRoute();
}
