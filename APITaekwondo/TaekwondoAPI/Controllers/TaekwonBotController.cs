using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TaekwondoAPI.Contracts;
using TaekwondoAPI.Services.TaekwonBot;

namespace TaekwondoAPI.Controllers;

[ApiController]
[Route("api/taekwonbot")]
public sealed class TaekwonBotController(BotService bot) : ControllerBase
{
    private string Owner => User.FindFirstValue("sub") ?? throw new BotException(401, "session_required", "Iniciá sesión.");
    private string Session => User.FindFirstValue("sid") ?? throw new BotException(401, "session_required", "Volvé a iniciar sesión.");
    [HttpGet("students")] public Task<IReadOnlyList<StudentChoice>> Students([FromQuery] string? query) => bot.Students(query, Owner, Session);
    [HttpGet("legacy/student/{id:int}")] public Task<StudentChoice> LegacyStudent(int id) => bot.LegacyStudent(id, Owner, Session);
    [HttpGet("legacy/payment/{id:int}")] public Task<PaymentView> LegacyPayment(int id) => bot.LegacyPayment(id, Owner, Session);
    [HttpGet("payments")] public Task<IReadOnlyList<PaymentView>> Payments([FromQuery] string? studentRef) => bot.Payments(studentRef, Owner, Session);
    [HttpGet("debts")] public Task<DebtResult> Debts([FromQuery] string? studentRef) => bot.Debt(studentRef, Owner, Session);
    [HttpGet("statistics")] public Task<StatisticsResult> Statistics([FromQuery] int year, [FromQuery] int month) => bot.Statistics(year, month);
    [HttpGet("history")] public Task<HistoryPage> History() => bot.History(Owner);
    [HttpDelete("history")] public async Task<IActionResult> DeleteHistory() { await bot.DeleteHistory(Owner); return NoContent(); }
    [HttpPost("chat")] public Task<ChatResponse> Chat(ChatRequest request, CancellationToken cancellationToken) => bot.Chat(request, Owner, Session, cancellationToken);
    [HttpPost("payments/prepare")] public Task<OperationView> Prepare(PreparePaymentRequest request) => bot.Prepare(request, Owner, Session);
    [HttpGet("operations/{reference}")] public Task<OperationView> Operation(string reference) => bot.Operation(reference, Owner, Session);
    [HttpPost("operations/{reference}/confirm")] public Task<OperationView> Confirm(string reference) => bot.Operation(reference, Owner, Session, "confirm");
    [HttpPost("operations/{reference}/cancel")] public Task<OperationView> Cancel(string reference) => bot.Operation(reference, Owner, Session, "cancel");
}
