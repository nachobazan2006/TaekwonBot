namespace BlazorTaekwondo.Models;

public sealed record StudentChoice(string StudentRef, string DisplayName, string Graduacion, string Horario, DateTime RegisteredAt);
public sealed record BotPeriod(int Year, int Month);
public sealed record PaymentView(string PaymentRef, string StudentRef, string StudentName, int Year, int Month,
    int Amount, string Method, DateTime Date, string Status, int Version);
public sealed record DebtResult(string StudentName, int FromYear, int FromMonth, int ToYear, int ToMonth, List<BotPeriod> PendingMonths);
public sealed record StatisticsResult(int Year, int Month, int Students, int Payments, int TotalAmount, int PendingStudents);
public sealed record HistoryMessage(long Id, string Role, string Text, DateTime CreatedAt);
public sealed record HistoryPage(List<HistoryMessage> Messages, int RetentionDays);
public sealed record PreparePaymentRequest(string Kind, string? StudentRef = null, string? PaymentRef = null,
    int? Year = null, int? Month = null, int? Amount = null, string? Method = null, DateTime? Date = null, string? Reason = null);
public sealed record OperationView(string OperationRef, string Kind, string Status, string Summary, DateTime ExpiresAt,
    PaymentView? Before = null, PaymentView? After = null);
public sealed record ChatRequest(string Text, string? StudentRef = null, int? Year = null, int? Month = null,
    string? Intent = null, string? MockScenario = null);
public sealed record ChatResponse(string State, string Message, string Provider, List<StudentChoice>? Students = null,
    DebtResult? Debt = null, List<PaymentView>? Payments = null, StatisticsResult? Statistics = null, OperationView? Operation = null,
    List<StudentChoice>? StudentCandidates = null, OperationView? PendingOperation = null, bool ConfirmationRequired = false);
