using DevTools.UI.Controls;
using System.Collections.Specialized;

namespace DevTools.HandyControl.Tests;

[TestClass]
public sealed class FilteredItemsTests
{
    [TestMethod]
    public void NarrowingKeepsSourceOrder()
    {
        var items = new FilteredItems(Words());

        items.Apply(Contains("l"));

        CollectionAssert.AreEqual(new[] { "alpha", "delta", "epsilon" }, items.ToList());
    }

    [TestMethod]
    public void WideningRestoresSourceOrder()
    {
        var items = new FilteredItems(Words());
        items.Apply(Contains("pp"));

        items.Apply(null);

        CollectionAssert.AreEqual(Words(), items.ToList());
    }

    [TestMethod]
    public void SmallChangeSendsSingleEdits()
    {
        var items = new FilteredItems(Words());
        var actions = Record(items);

        items.Apply(Contains("l"));

        CollectionAssert.AreEqual(new[] { NotifyCollectionChangedAction.Remove, NotifyCollectionChangedAction.Remove, NotifyCollectionChangedAction.Remove },
            actions);
    }

    [TestMethod]
    public void SameResultSendsNothing()
    {
        var items = new FilteredItems(Words());
        items.Apply(Contains("l"));
        var actions = Record(items);

        items.Apply(Contains("l"));

        Assert.AreEqual(0, actions.Count);
    }

    [TestMethod]
    public void LargeChangeSendsReset()
    {
        var source = Enumerable.Range(0, FilteredItems.MaxEdits * 2).Select(index => (object)("item " + index)).ToList();
        var items = new FilteredItems(source);
        var actions = Record(items);

        items.Apply(item => item.Equals("item 3"));

        CollectionAssert.AreEqual(new[] { NotifyCollectionChangedAction.Reset }, actions);
        CollectionAssert.AreEqual(new[] { "item 3" }, items.ToList());
    }

    [TestMethod]
    public void ExactMatchComesFirst()
    {
        var items = new FilteredItems(new List<object> { "duct width", "width", "width offset" });

        items.Apply(Contains("width"), item => item.Equals("width"));

        CollectionAssert.AreEqual(new[] { "width", "duct width", "width offset" }, items.ToList());
    }

    [TestMethod]
    public void NoExactMatchKeepsSourceOrder()
    {
        var items = new FilteredItems(new List<object> { "duct width", "width", "width offset" });
        items.Apply(Contains("width"), item => item.Equals("width"));

        items.Apply(Contains("widt"));

        CollectionAssert.AreEqual(new[] { "duct width", "width", "width offset" }, items.ToList());
    }

    private static List<object> Words()
    {
        return ["alpha", "beta", "gamma", "delta", "epsilon", "kappa"];
    }

    private static Predicate<object> Contains(string text)
    {
        return item => ((string)item).Contains(text);
    }

    private static List<NotifyCollectionChangedAction> Record(FilteredItems items)
    {
        var actions = new List<NotifyCollectionChangedAction>();
        items.CollectionChanged += (_, e) => actions.Add(e.Action);
        return actions;
    }
}
