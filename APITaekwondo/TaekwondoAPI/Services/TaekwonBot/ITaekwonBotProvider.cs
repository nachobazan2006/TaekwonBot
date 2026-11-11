namespace TaekwondoAPI.Services.TaekwonBot;

// DTO cerrado: jamás admite texto original, identidades ni registros del dominio.
public sealed record ProviderRequest(string Intent, string Scenario, int? Year, int? Month, int? AggregateSubjects = null);
public sealed record ProviderUsage(string Model, int InputTokens, int CachedInputTokens, int OutputTokens, decimal CostUsd);
public sealed record ProviderResponse(string Message, ProviderUsage? Usage = null);

public interface ITaekwonBotProvider
{
    string Name { get; }
    Task<ProviderResponse> RespondAsync(ProviderRequest request, CancellationToken cancellationToken);
}

public static class ProviderPayloadFactory
{
    public static ProviderRequest Create(string intent, string? scenario, int? year, int? month, int? subjects = null)
    {
        if (intent is not ("students" or "payments" or "debts" or "statistics" or "help")) throw new ArgumentException("Intención no autorizada.");
        if (scenario is not (null or "success" or "error" or "timeout" or "no_students" or "ambiguous" or "duplicate")) throw new ArgumentException("Escenario no autorizado.");
        if (subjects is < 5) subjects = null;
        return new(intent, scenario ?? "success", year, month, subjects);
    }
}

public sealed class MockTaekwonBotProvider : ITaekwonBotProvider
{
    public string Name => "mock";
    public Task<ProviderResponse> RespondAsync(ProviderRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.Scenario == "timeout") throw new TimeoutException();
        if (request.Scenario == "error") throw new InvalidOperationException("mock_provider_failure");
        return Task.FromResult(new ProviderResponse(request.Intent == "help"
            ? "Elegí una consulta, un alumno o una operación de pago. Cada cambio necesita tu confirmación."
            : "Consulta procesada. Los datos se muestran únicamente en esta aplicación local."));
    }
}
