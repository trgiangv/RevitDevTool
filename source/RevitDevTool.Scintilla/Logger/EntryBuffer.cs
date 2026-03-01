using System.Buffers;
using RevitDevTool.Scintilla.Core;
namespace RevitDevTool.Scintilla.Logger;

internal readonly struct EntryBuffer
{
    public static EntryBuffer Empty { get; } = new(Array.Empty<LogEntry>(), 0, false);

    public EntryBuffer(LogEntry[] entries, int count, bool rented)
    {
        Entries = entries;
        Count = count;
        Rented = rented;
    }

    public LogEntry[] Entries { get; }
    public int Count { get; }
    public bool Rented { get; }

    public static EntryBuffer CopyFrom(IReadOnlyList<LogEntry> source)
    {
        if (source.Count == 0)
            return Empty;

        var rented = ArrayPool<LogEntry>.Shared.Rent(source.Count);
        for (var i = 0; i < source.Count; i++)
            rented[i] = source[i];

        return new EntryBuffer(rented, source.Count, true);
    }

    public void Return()
    {
        if (!Rented || Entries.Length == 0)
            return;

        Array.Clear(Entries, 0, Entries.Length);
        ArrayPool<LogEntry>.Shared.Return(Entries);
    }
}
