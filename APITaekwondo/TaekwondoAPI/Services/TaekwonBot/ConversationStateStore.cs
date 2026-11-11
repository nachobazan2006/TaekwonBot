using System.Collections.Concurrent;
using TaekwondoAPI.Contracts;

namespace TaekwondoAPI.Services.TaekwonBot;

public sealed class ConversationStateStore
{
    private readonly ConcurrentDictionary<string, ConversationState> states = new();
    public ConversationState Get(string owner, string session)
    {
        var key = owner + "\n" + session;
        var now = DateTimeOffset.UtcNow;
        var state = states.GetOrAdd(key, _ => new());
        if (state.LastUsedAt.AddMinutes(30) < now)
        {
            state = new();
            states[key] = state;
        }
        state.LastUsedAt = now;
        return state;
    }
    public void Delete(string owner)
    {
        foreach (var key in states.Keys.Where(key => key.StartsWith(owner + "\n", StringComparison.Ordinal)))
            states.TryRemove(key, out _);
    }
}

public sealed class ConversationState
{
    public List<StudentChoice> Candidates { get; set; } = [];
    public string? SelectedStudentRef { get; set; }
    public int? Year { get; set; }
    public int? Month { get; set; }
    public int? Amount { get; set; }
    public string? Method { get; set; }
    public string? ReadIntent { get; set; }
    public string? ChangeKind { get; set; }
    public string? ChangeReason { get; set; }
    public string? PendingOperationRef { get; set; }
    public DateTimeOffset LastUsedAt { get; set; } = DateTimeOffset.UtcNow;
    public SemaphoreSlim Gate { get; } = new(1, 1);
}
