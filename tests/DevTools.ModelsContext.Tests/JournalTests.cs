using DevTools.ModelsContext.Engine;
using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Tests.Shared;

namespace DevTools.ModelsContext.Tests;

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
        journal.Record(Current, View, [new JournalEntry(Linked, "uid-7", VisibilityAction.Hide)]);

        var state = journal.Get(Current, View);
        Assert.AreEqual(2, state.ChangeCount);
        Assert.AreEqual(VisibilityAction.Hide, journal.ActionOf(Current, View, Linked));
    }

    [TestMethod]
    public void ResetClearsJournal()
    {
        var journal = new TemporaryJournal();
        journal.Record(Current, View, [new JournalEntry(Linked, "uid-7", VisibilityAction.Hide)]);

        journal.Reset(Current, View);

        Assert.AreEqual(JournalState.Empty, journal.Get(Current, View));
    }

    [TestMethod]
    public void UndoMarksJournalStale()
    {
        var journal = new TemporaryJournal();
        journal.Record(Current, View, [new JournalEntry(Linked, "uid-7", VisibilityAction.Hide)]);

        journal.MarkStale(Current);

        Assert.IsTrue(journal.Get(Current, View).IsStale);
        Assert.IsNull(journal.ActionOf(Current, View, Linked));
    }

    [TestMethod]
    public void LinkReloadClearsItsEntries()
    {
        var journal = new TemporaryJournal();
        journal.Record(Current, View, [new JournalEntry(Linked, "uid-7", VisibilityAction.Hide)]);

        journal.ClearLink(FakeModelGenerator.Mep);

        Assert.IsEmpty(journal.Get(Current, View).LinkEntries);
    }
}
