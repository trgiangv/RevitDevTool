using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Tests.Shared;
using DevTools.ModelsContext.Visibility;

namespace DevTools.ModelsContext.Tests.Visibility;

[TestClass]
public sealed class JournalTests
{
    private const string View = "view-1";
    private static readonly DocumentKey Current = FakeModelGenerator.Current;
    private static readonly ElementRef Linked = new(FakeModelGenerator.Mep, 7);

    [TestMethod]
    public void JournalCountsChanges()
    {
        var journal = new TemporaryJournal();

        journal.Record(Current, View);
        journal.RecordHidden(Current, View, [new JournalEntry(Linked, "uid-7")]);

        var state = journal.Get(Current, View);
        Assert.AreEqual(2, state.ChangeCount);
        Assert.AreEqual(Linked, state.LinkEntries.Single().Element);
    }

    [TestMethod]
    public void ResetClearsJournal()
    {
        var journal = new TemporaryJournal();
        journal.RecordHidden(Current, View, [new JournalEntry(Linked, "uid-7")]);

        journal.Reset(Current, View);

        Assert.AreEqual(JournalState.Empty, journal.Get(Current, View));
    }

    [TestMethod]
    public void TemporaryResetKeepsLinkHides()
    {
        var journal = new TemporaryJournal();
        journal.Record(Current, View);
        journal.RecordHidden(Current, View, [new JournalEntry(Linked, "uid-7")]);

        journal.ResetTemporary(Current, View);

        var state = journal.Get(Current, View);
        Assert.AreEqual(1, state.ChangeCount);
        Assert.HasCount(1, state.LinkEntries);
    }

    [TestMethod]
    public void RemoveDropsShownEntries()
    {
        var journal = new TemporaryJournal();
        journal.RecordHidden(Current, View, [new JournalEntry(Linked, "uid-7")]);

        journal.Remove(Current, View, [Linked]);

        Assert.AreEqual(JournalState.Empty, journal.Get(Current, View));
    }

    [TestMethod]
    public void UndoMarksJournalStale()
    {
        var journal = new TemporaryJournal();
        journal.RecordHidden(Current, View, [new JournalEntry(Linked, "uid-7")]);

        journal.MarkStale(Current);

        Assert.IsTrue(journal.Get(Current, View).IsStale);
    }

    [TestMethod]
    public void LinkReloadClearsItsEntries()
    {
        var journal = new TemporaryJournal();
        journal.RecordHidden(Current, View, [new JournalEntry(Linked, "uid-7")]);

        journal.ClearLink(FakeModelGenerator.Mep);

        Assert.IsEmpty(journal.Get(Current, View).LinkEntries);
    }
}
