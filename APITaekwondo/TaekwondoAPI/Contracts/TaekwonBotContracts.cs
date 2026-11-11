namespace TaekwondoAPI.Contracts;

public sealed record StudentChoice(string StudentRef, string DisplayName, string Graduacion, string Horario, DateTime RegisteredAt);
public sealed record Period(int Year, int Month);
public sealed record PaymentView(string PaymentRef, string StudentRef, string StudentName, int Year, int Month, int Amount, string Method, DateTime Date, string Status, int Version);
public sealed record DebtResult(string StudentName, int FromYear, int FromMonth, int ToYear, int ToMonth, IReadOnlyList<Period> PendingMonths);
public sealed record StatisticsResult(int Year, int Month, int Students, int Payments, int TotalAmount, int PendingStudents);
public sealed record HistoryMessage(long Id, string Role, string Text, DateTime CreatedAt);
public sealed record HistoryPage(IReadOnlyList<HistoryMessage> Messages, int RetentionDays);
public sealed record PreparePaymentRequest(string Kind, string? StudentRef = null, string? PaymentRef = null, int? Year = null, int? Month = null, int? Amount = null, string? Method = null, DateTime? Date = null, string? Reason = null);
public sealed record OperationView(string OperationRef, string Kind, string Status, string Summary, DateTime ExpiresAt, PaymentView? Before = null, PaymentView? After = null);
public sealed record ChatRequest(string Text, string? StudentRef = null, int? Year = null, int? Month = null, string? Intent = null, string? MockScenario = null);
public sealed record ChatResponse(string State, string Message, string Provider, IReadOnlyList<StudentChoice>? Students = null, DebtResult? Debt = null, IReadOnlyList<PaymentView>? Payments = null, StatisticsResult? Statistics = null, OperationView? Operation = null, IReadOnlyList<StudentChoice>? StudentCandidates = null, OperationView? PendingOperation = null, bool ConfirmationRequired = false);
public sealed record BotError(string Code, string Message);

public sealed class BotException(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}
