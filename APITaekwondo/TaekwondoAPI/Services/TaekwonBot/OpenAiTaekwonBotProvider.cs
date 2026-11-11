using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TaekwondoAPI.Services.TaekwonBot;

// Solo consume contexto canónico; nunca recibe texto libre ni entidades de alumnos.
public sealed class OpenAiTaekwonBotProvider(HttpClient httpClient, IConfiguration configuration) : ITaekwonBotProvider
{
    public const string Snapshot = "gpt-5.4-mini-2026-03-17";
    public const string ReadyMessage = "La consulta está lista; revisá los resultados del panel.";
    private const string ToolName = "get_local_summary";
    private static readonly Uri Endpoint = new("https://api.openai.com/v1/responses");
    private static readonly HashSet<string> Intents = ["students", "payments", "debts", "statistics", "help"];
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public string Name => "openai";

    public async Task<ProviderResponse> RespondAsync(ProviderRequest request, CancellationToken cancellationToken)
    {
        var apiKey = configuration["TaekwonBot:OpenAi:ApiKey"];
        var model = configuration["TaekwonBot:OpenAi:Model"];
        if (!configuration.GetValue<bool>("TaekwonBot:OpenAi:Enabled") || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(model))
            throw new OpenAiProviderException("OPENAI_NOT_CONFIGURED", "BLOCKED_NEEDS_USER: configurá clave, modelo y habilitación fuera del chat.");
        if (model is not (Snapshot or "gpt-5.4-mini"))
            throw new OpenAiProviderException("OPENAI_MODEL_NOT_VERIFIED", "El modelo configurado requiere verificar compatibilidad y precios antes de usarlo.");
        if (!Intents.Contains(request.Intent) || request.Scenario != "success"
            || request.Year is < 1900 or > 2200 || request.Month is < 1 or > 12 || request.AggregateSubjects is < 0)
            throw new OpenAiProviderException("UNSAFE_PROVIDER_CONTEXT", "El contexto no pertenece al esquema autorizado; no se envió a OpenAI.");

        var context = JsonSerializer.Serialize(new
        {
            intent = request.Intent,
            year = request.Year,
            month = request.Month,
            aggregateSubjects = request.AggregateSubjects >= 5 ? request.AggregateSubjects : null
        }, Json);
        object[] input = [new { role = "user", content = context }];
        var tool = new
        {
            type = "function", name = ToolName,
            description = "Read the already authorized, deidentified local query context. No identities or write actions are available.",
            strict = true,
            parameters = new
            {
                type = "object",
                properties = new
                {
                    intent = new { type = "string", @enum = new[] { request.Intent } },
                    year = new { type = new[] { "integer", "null" }, @enum = new int?[] { request.Year } },
                    month = new { type = new[] { "integer", "null" }, @enum = new int?[] { request.Month } }
                },
                required = new[] { "intent", "year", "month" }, additionalProperties = false
            }
        };
        var firstBody = new
        {
            model, store = false, reasoning = new { effort = "none" }, max_output_tokens = 512,
            instructions = "Call the authorized read-only tool with exactly the supplied intent and period. Never request identity or perform writes.",
            input, tools = new[] { tool }, tool_choice = new { type = "function", name = ToolName }, parallel_tool_calls = false
        };
        ProviderUsage? usage = null;
        try
        {
            using var first = await SendAsync(firstBody, apiKey, cancellationToken);
            usage = ReadUsage(first.RootElement, model);
            var calls = first.RootElement.GetProperty("output").EnumerateArray()
                .Where(item => item.TryGetProperty("type", out var type) && type.GetString() == "function_call").ToArray();
            if (calls.Length != 1 || calls[0].GetProperty("name").GetString() != ToolName)
                throw new OpenAiProviderException("INVALID_MODEL_TOOL", "La respuesta no contiene una consulta permitida.", usage);
            var callId = calls[0].GetProperty("call_id").GetString() ?? "";
            if (!Regex.IsMatch(callId, "^[A-Za-z0-9_-]{1,160}$"))
                throw new OpenAiProviderException("INVALID_MODEL_TOOL", "La respuesta de herramienta no es válida.", usage);
            using var arguments = JsonDocument.Parse(calls[0].GetProperty("arguments").GetString() ?? "{}");
            ValidateArguments(arguments.RootElement, request, usage);
            // Reconstruir los argumentos: no reenviar mensajes o campos arbitrarios del modelo.
            var approvedArguments = JsonSerializer.Serialize(new { intent = request.Intent, year = request.Year, month = request.Month }, Json);
            object[] continuedInput = [input[0],
                new { type = "function_call", call_id = callId, name = ToolName, arguments = approvedArguments },
                new { type = "function_call_output", call_id = callId, output = context }];
            var finalBody = new
            {
                model, store = false, reasoning = new { effort = "none" }, max_output_tokens = 512,
                instructions = "Return the allowed readiness message. Identifiable results are rendered separately by the local application.",
                input = continuedInput, tools = new[] { tool }, tool_choice = "none", parallel_tool_calls = false,
                text = new { format = new
                {
                    type = "json_schema", name = "local_summary_response", strict = true,
                    schema = new
                    {
                        type = "object", properties = new { message = new { type = "string", @enum = new[] { ReadyMessage } } },
                        required = new[] { "message" }, additionalProperties = false
                    }
                }}
            };
            using var final = await SendAsync(finalBody, apiKey, cancellationToken);
            usage = AddUsage(usage, ReadUsage(final.RootElement, model));
            var text = final.RootElement.GetProperty("output").EnumerateArray()
                .Where(item => item.TryGetProperty("type", out var type) && type.GetString() == "message")
                .SelectMany(item => item.GetProperty("content").EnumerateArray())
                .Where(item => item.TryGetProperty("type", out var type) && type.GetString() == "output_text")
                .Select(item => item.GetProperty("text").GetString()).SingleOrDefault();
            using var answer = JsonDocument.Parse(text ?? "{}");
            if (answer.RootElement.EnumerateObject().Count() != 1
                || !answer.RootElement.TryGetProperty("message", out var message) || message.GetString() != ReadyMessage)
                throw new OpenAiProviderException("INVALID_MODEL_ANSWER", "La respuesta del proveedor no cumple el formato permitido.", usage);
            return new ProviderResponse(ReadyMessage, usage);
        }
        catch (OpenAiProviderException error) when (error.Usage is null && usage is not null)
        { throw new OpenAiProviderException(error.Code, error.Message, usage); }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException)
        { throw new OpenAiProviderException("INVALID_MODEL_RESPONSE", "El proveedor devolvió una respuesta no válida.", usage); }
    }

    private async Task<JsonDocument> SendAsync(object body, string apiKey, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        using var message = new HttpRequestMessage(HttpMethod.Post, Endpoint);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        message.Content = new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json");
        try
        {
            using var response = await httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                var code = response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "OPENAI_ACCESS_DENIED",
                    HttpStatusCode.TooManyRequests => "OPENAI_QUOTA_OR_RATE_LIMIT",
                    _ => "OPENAI_UNAVAILABLE"
                };
                // No registrar ni propagar el body externo, que puede contener información sensible.
                throw new OpenAiProviderException(code, "OpenAI no está disponible para esta consulta; el estado de los pagos no cambió.");
            }
            if (response.Content.Headers.ContentLength > 262144)
                throw new OpenAiProviderException("OPENAI_RESPONSE_TOO_LARGE", "La respuesta del proveedor excede el límite permitido.");
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var limited = new MemoryStream();
            var buffer = new byte[8192];
            int length;
            while ((length = await stream.ReadAsync(buffer, timeout.Token)) > 0)
            {
                if (limited.Length + length > 262144)
                    throw new OpenAiProviderException("OPENAI_RESPONSE_TOO_LARGE", "La respuesta del proveedor excede el límite permitido.");
                limited.Write(buffer, 0, length);
            }
            var document = JsonDocument.Parse(limited.ToArray());
            if (!document.RootElement.TryGetProperty("status", out var status) || status.GetString() != "completed")
            { document.Dispose(); throw new OpenAiProviderException("OPENAI_INCOMPLETE", "El proveedor no completó la consulta."); }
            return document;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new OpenAiProviderException("OPENAI_TIMEOUT", "OpenAI tardó demasiado; no se reintentó ninguna escritura."); }
        catch (HttpRequestException)
        { throw new OpenAiProviderException("OPENAI_NETWORK_ERROR", "No se pudo contactar OpenAI; los datos locales permanecen disponibles."); }
    }

    private static void ValidateArguments(JsonElement arguments, ProviderRequest request, ProviderUsage usage)
    {
        if (arguments.ValueKind != JsonValueKind.Object || arguments.EnumerateObject().Count() != 3
            || !arguments.TryGetProperty("intent", out var intent) || intent.GetString() != request.Intent
            || !arguments.TryGetProperty("year", out var year) || ReadNullable(year) != request.Year
            || !arguments.TryGetProperty("month", out var month) || ReadNullable(month) != request.Month)
            throw new OpenAiProviderException("INVALID_MODEL_ARGUMENTS", "El modelo propuso parámetros distintos a la consulta autorizada.", usage);
    }

    private static int? ReadNullable(JsonElement value) => value.ValueKind == JsonValueKind.Null ? null : value.GetInt32();
    private static ProviderUsage ReadUsage(JsonElement root, string model)
    {
        var value = root.GetProperty("usage");
        var input = value.GetProperty("input_tokens").GetInt32();
        var output = value.GetProperty("output_tokens").GetInt32();
        var cached = value.TryGetProperty("input_tokens_details", out var details)
            && details.TryGetProperty("cached_tokens", out var cache) ? cache.GetInt32() : 0;
        if (input < 0 || output < 0 || cached < 0 || cached > input)
            throw new JsonException("Invalid usage.");
        var cost = ((input - cached) * .75m + cached * .075m + output * 4.50m) / 1_000_000m;
        return new ProviderUsage(model, input, cached, output, cost);
    }
    private static ProviderUsage AddUsage(ProviderUsage first, ProviderUsage second) => new(first.Model,
        first.InputTokens + second.InputTokens, first.CachedInputTokens + second.CachedInputTokens,
        first.OutputTokens + second.OutputTokens, first.CostUsd + second.CostUsd);
}

public sealed class OpenAiProviderException(string code, string message, ProviderUsage? usage = null) : Exception(message)
{
    public string Code { get; } = code;
    public ProviderUsage? Usage { get; } = usage;
}
