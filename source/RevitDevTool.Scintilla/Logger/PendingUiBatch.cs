using System.Buffers;
using System.Collections;
using RevitDevTool.Scintilla.Core;
namespace RevitDevTool.Scintilla.Logger;

internal sealed class PendingUiBatch : IReadOnlyList<LogEntry>
{
    public PendingUiBatch(
        long epoch,
        EntryBuffer releaseEntries,
        EntryBuffer visibleEntries,
        bool autoScroll)
    {
        Epoch = epoch;
        VisibleEntries = visibleEntries;
        EntriesToRelease = releaseEntries;
        AutoScroll = autoScroll;
    }

    public long Epoch { get; }
    public EntryBuffer VisibleEntries { get; }
    public EntryBuffer EntriesToRelease { get; }
    public bool AutoScroll { get; }

    public int Count => VisibleEntries.Count;

    public LogEntry this[int index] => VisibleEntries.Entries[index];

    public IEnumerator<LogEntry> GetEnumerator()
    {
        for (var i = 0; i < VisibleEntries.Count; i++)
            yield return VisibleEntries.Entries[i];
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void ReleaseEntries()
    {
        if (EntriesToRelease.Count > 0)
        {
            for (var i = 0; i < EntriesToRelease.Count; i++)
                EntriesToRelease.Entries[i].ReleaseBuffer();

            if (EntriesToRelease.Rented)
            {
                Array.Clear(EntriesToRelease.Entries, 0, EntriesToRelease.Entries.Length);
                ArrayPool<LogEntry>.Shared.Return(EntriesToRelease.Entries);
            }
        }

        if (VisibleEntries.Rented && !ReferenceEquals(VisibleEntries.Entries, EntriesToRelease.Entries))
        {
            Array.Clear(VisibleEntries.Entries, 0, VisibleEntries.Entries.Length);
            ArrayPool<LogEntry>.Shared.Return(VisibleEntries.Entries);
        }
    }
}
