using BlazorTaekwondo.Models;
using System.Net.Http.Json;

namespace BlazorTaekwondo.Services;

public sealed class BotApiException(int statusCode, string code, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
    public bool StatusUnknown => StatusCode is 408 or 504 || Code.Equals("STATUS_UNKNOWN", StringComparison.OrdinalIgnoreCase);
}

public sealed class TaekwonBotService(HttpClient http)
{
    private const string Root = "api/taekwonbot/";
    public Task<List<StudentChoice>> Students(string query = "") => Get<List<StudentChoice>>(Root + "students?query=" + Uri.EscapeDataString(query));
    public Task<HistoryPage> History() => Get<HistoryPage>(Root + "history");
    public async Task ClearHistory() => await Check(await http.DeleteAsync(Root + "history"));
    public Task<ChatResponse> Chat(ChatRequest request) => Post<ChatResponse>(Root + "chat", request);
    public Task<DebtResult> Debts(string studentRef) => Get<DebtResult>(Root + "debts?studentRef=" + Uri.EscapeDataString(studentRef));
    public Task<List<PaymentView>> Payments(string studentRef) => Get<List<PaymentView>>(Root + "payments?studentRef=" + Uri.EscapeDataString(studentRef));
    public Task<StatisticsResult> Statistics(int year, int month) => Get<StatisticsResult>($"{Root}statistics?year={year}&month={month}");
    public Task<OperationView> Prepare(PreparePaymentRequest request) => Post<OperationView>(Root + "payments/prepare", request);
    public Task<OperationView> Confirm(string operationRef) => Post<OperationView>(Root + "operations/" + Uri.EscapeDataString(operationRef) + "/confirm", new { });
    public Task<OperationView> Cancel(string operationRef) => Post<OperationView>(Root + "operations/" + Uri.EscapeDataString(operationRef) + "/cancel", new { });
    public Task<OperationView> Status(string operationRef) => Get<OperationView>(Root + "operations/" + Uri.EscapeDataString(operationRef));
    public Task<StudentChoice> LegacyStudent(int id) => Get<StudentChoice>($"{Root}legacy/student/{id}");
    public Task<PaymentView> LegacyPayment(int id) => Get<PaymentView>($"{Root}legacy/payment/{id}");

    private async Task<T> Get<T>(string path)
    {
        using var response = await http.GetAsync(path);
        await Check(response);
        return await response.Content.ReadFromJsonAsync<T>() ?? throw new BotApiException(502, "INVALID_RESPONSE", "No se pudo interpretar la respuesta local.");
    }
    private async Task<T> Post<T>(string path, object request)
    {
        using var response = await http.PostAsJsonAsync(path, request);
        await Check(response);
        return await response.Content.ReadFromJsonAsync<T>() ?? throw new BotApiException(502, "INVALID_RESPONSE", "No se pudo interpretar la respuesta local.");
    }
    private static async Task Check(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        ApiError? error = null;
        try { error = await response.Content.ReadFromJsonAsync<ApiError>(); } catch (System.Text.Json.JsonException) { }
        throw new BotApiException((int)response.StatusCode, error?.Code ?? "REQUEST_FAILED",
            error?.Message ?? "No se pudo completar la solicitud. Revisá los datos e intentá nuevamente.");
    }
    private sealed record ApiError(string Code, string Message);
}
