namespace TaekwondoAPI.Models;

public sealed class BotHistoryMessage
{
    public long Id { get; set; }
    public string Owner { get; set; } = "";
    public string Role { get; set; } = "";
    public string Text { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public sealed class BotOperation
{
    public string Id { get; set; } = "";
    public string Owner { get; set; } = "";
    public string Session { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Status { get; set; } = "prepared";
    public int? PaymentId { get; set; }
    public int ExpectedVersion { get; set; }
    public string BeforeJson { get; set; } = "null";
    public string AfterJson { get; set; } = "null";
    public string Reason { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}

public sealed class PaymentAuditEvent
{
    public long Id { get; set; }
    public string OperationId { get; set; } = "";
    public int PaymentId { get; set; }
    public string Owner { get; set; } = "";
    public string Kind { get; set; } = "";
    public string Reason { get; set; } = "";
    public string BeforeJson { get; set; } = "null";
    public string AfterJson { get; set; } = "null";
    public DateTime CreatedAt { get; set; }
}
