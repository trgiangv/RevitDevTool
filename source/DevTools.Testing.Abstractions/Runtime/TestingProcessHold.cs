namespace DevTools.Testing.Abstractions.Runtime;

/// <summary>
/// Process-wide slot bag on the parent-bound Abstractions assembly.
/// Isolation may <c>LoadFile</c> extra Runtime copies per generation while a
/// framework assembly stays identity-bound; those copies must share parked
/// catalog state through this type, not through Runtime statics.
/// </summary>
public static class TestingProcessHold
{
    public static readonly object Gate = new();
    private static readonly Dictionary<string, object> Slots = new(StringComparer.Ordinal);

    public static T GetOrAdd<T>(string key, Func<T> create) where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(create);

        lock (Gate)
        {
            if (Slots.TryGetValue(key, out var existing))
            {
                if (existing is T typed)
                    return typed;
                return create();
            }

            var created = create()
                ?? throw new InvalidOperationException($"Hold factory for '{key}' returned null.");
            Slots[key] = created;
            return created;
        }
    }
}
