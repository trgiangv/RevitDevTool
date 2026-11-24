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
        var journal = new ChangeJournal();

        journal.AddChange(Current, View, true);
        journal.AddLinkHides(Current, View, [new LinkHide(Linked, "uid-7")]);

        var page = journal.Get(Current, View);
        Assert.AreEqual(2, page.ChangeCount);
        Assert.AreEqual(Linked, page.LinkHides.Single().Element);
    }

    [TestMethod]
    public void KeepClearsConvertedChanges()
    {
        var journal = new ChangeJournal();
        journal.AddChange(Current, View, true);
        journal.AddLinkHides(Current, View, [new LinkHide(Linked, "uid-7")]);

        journal.Keep(Current, View);

        Assert.AreEqual(JournalPage.Empty, journal.Get(Current, View));
    }

    [TestMethod]
    public void KeepLeavesPropertyChanges()
    {
        var journal = new ChangeJournal();
        journal.AddChange(Current, View, true);
        journal.AddChange(Current, View, false);

        journal.Keep(Current, View);

        Assert.AreEqual(1, journal.Get(Current, View).ChangeCount);
    }

    [TestMethod]
    public void RenameMovesPages()
    {
        var journal = new ChangeJournal();
        journal.AddLinkHides(Current, View, [new LinkHide(Linked, "uid-7")]);
        var rename = new ModelRename(Current.ModelId, "renamed.rvt", FakeModelGenerator.CurrentSource,
            FakeModelGenerator.CurrentSource);

        journal.Rename(rename);

        Assert.AreEqual(JournalPage.Empty, journal.Get(Current, View));
        Assert.AreEqual(rename.Map(Linked),
            journal.Get(DocumentKey.Current("renamed.rvt"), View).LinkHides.Single().Element);
    }

    [TestMethod]
    public void TemporaryResetKeepsLinkHides()
    {
        var journal = new ChangeJournal();
        journal.AddChange(Current, View, true);
        journal.AddLinkHides(Current, View, [new LinkHide(Linked, "uid-7")]);

        journal.ClearChanges(Current, View);

        var page = journal.Get(Current, View);
        Assert.AreEqual(1, page.ChangeCount);
        Assert.HasCount(1, page.LinkHides);
    }

    [TestMethod]
    public void RemoveDropsShownEntries()
    {
        var journal = new ChangeJournal();
        journal.AddLinkHides(Current, View, [new LinkHide(Linked, "uid-7")]);

        journal.Remove(Current, View, [Linked]);

        Assert.AreEqual(JournalPage.Empty, journal.Get(Current, View));
    }

    [TestMethod]
    public void UndoMarksJournalStale()
    {
        var journal = new ChangeJournal();
        journal.AddLinkHides(Current, View, [new LinkHide(Linked, "uid-7")]);

        journal.MarkStale(Current);

        Assert.IsTrue(journal.Get(Current, View).IsStale);
    }

    [TestMethod]
    public void LinkReloadClearsItsEntries()
    {
        var journal = new ChangeJournal();
        journal.AddLinkHides(Current, View, [new LinkHide(Linked, "uid-7")]);

        journal.ClearLink(FakeModelGenerator.Mep);

        Assert.IsEmpty(journal.Get(Current, View).LinkHides);
    }
}
