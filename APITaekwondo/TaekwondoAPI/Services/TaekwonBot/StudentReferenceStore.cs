using System.Collections.Concurrent;
using System.Security.Cryptography;
using TaekwondoAPI.Contracts;

namespace TaekwondoAPI.Services.TaekwonBot;

public sealed class StudentReferenceStore(TimeProvider clock)
{
    private sealed record Reference(string Owner, string Session, string Kind, int Id, DateTimeOffset Expires);
    private readonly ConcurrentDictionary<string, Reference> references = new();
    public string Create(string owner, string session, string kind, int id)
    {
        var now = clock.GetUtcNow();
        foreach (var pair in references.Where(p => p.Value.Expires <= now)) references.TryRemove(pair.Key, out _);
        var existing = references.FirstOrDefault(p => p.Value.Owner == owner && p.Value.Session == session && p.Value.Kind == kind && p.Value.Id == id);
        if (existing.Key is not null) return existing.Key;
        if (references.Count > 100_000) throw new BotException(429, "reference_limit", "Hay demasiadas referencias activas. Intentá más tarde.");
        var key = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        references[key] = new(owner, session, kind, id, now.AddMinutes(30));
        return key;
    }
    public int Resolve(string? key, string owner, string session, string kind)
    {
        if (key is null || !references.TryGetValue(key, out var reference) || reference.Owner != owner || reference.Session != session || reference.Kind != kind || reference.Expires <= clock.GetUtcNow())
            throw new BotException(410, "selection_expired", "La selección venció o no corresponde a esta sesión. Volvé a seleccionar.");
        return reference.Id;
    }
}
