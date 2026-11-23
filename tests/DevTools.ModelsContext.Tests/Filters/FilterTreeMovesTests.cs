using DevTools.ModelsContext.Filters;

namespace DevTools.ModelsContext.Tests.Filters;

[TestClass]
public sealed class FilterTreeMovesTests
{
    [TestMethod]
    public void GroupCannotEnterItself()
    {
        var root = new Node(null);
        var outer = new Node(root);
        var inner = new Node(outer);

        Assert.IsFalse(CanMove(outer, outer));
        Assert.IsFalse(CanMove(outer, inner));
        Assert.IsTrue(CanMove(inner, root));
    }

    [TestMethod]
    public void ReadOnlyBlocksMove()
    {
        var root = new Node(null);
        var locked = new Node(root, true);
        var item = new Node(locked);
        var open = new Node(root);

        Assert.IsFalse(CanMove(item, open));
        Assert.IsFalse(CanMove(open, locked));
    }

    [TestMethod]
    public void DetachedItemCannotMove()
    {
        Assert.IsFalse(CanMove(new Node(null), new Node(null)));
    }

    [TestMethod]
    [DataRow(0, 2, 3, 1)]
    [DataRow(0, 3, 3, 2)]
    [DataRow(2, 0, 3, 0)]
    [DataRow(1, 9, 3, 2)]
    [DataRow(2, -1, 3, 0)]
    public void SameGroupIndex(int from, int index, int count, int expected)
    {
        Assert.AreEqual(expected, FilterTreeMoves.WithinGroup(from, index, count));
    }

    [TestMethod]
    [DataRow(1, 1)]
    [DataRow(1, 2)]
    [DataRow(2, 3)]
    public void SameSlotIsNoMove(int from, int index)
    {
        Assert.IsNull(FilterTreeMoves.WithinGroup(from, index, 3));
    }

    [TestMethod]
    public void OtherGroupIndexClamps()
    {
        Assert.AreEqual(0, FilterTreeMoves.IntoGroup(-4, 2));
        Assert.AreEqual(1, FilterTreeMoves.IntoGroup(1, 2));
        Assert.AreEqual(2, FilterTreeMoves.IntoGroup(9, 2));
    }

    [TestMethod]
    public void UpDownStepOneSlot()
    {
        Assert.AreEqual(0, FilterTreeMoves.WithinGroup(1, FilterTreeMoves.Up(1), 3));
        Assert.AreEqual(2, FilterTreeMoves.WithinGroup(1, FilterTreeMoves.Down(1), 3));
        Assert.IsFalse(FilterTreeMoves.CanMoveUp(0));
        Assert.IsFalse(FilterTreeMoves.CanMoveDown(2, 3));
        Assert.IsTrue(FilterTreeMoves.CanMoveDown(1, 3));
    }

    private static bool CanMove(Node item, Node target)
    {
        return FilterTreeMoves.CanMove(item, target, node => node.Parent, node => node.IsReadOnly);
    }

    private sealed class Node(Node? parent, bool isReadOnly = false)
    {
        public Node? Parent { get; } = parent;

        public bool IsReadOnly { get; } = isReadOnly;
    }
}
