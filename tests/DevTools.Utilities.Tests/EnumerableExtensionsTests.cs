using DevTools.Utilities;

namespace DevTools.Utilities.Tests;

public sealed class EnumerableExtensionsTests
{
    [Fact]
    public void Dispose_on_enumerable_disposes_each_item()
    {
        var first = new TrackingDisposable();
        var second = new TrackingDisposable();
        new IDisposable?[] { first, null, second }.Dispose();
        Assert.True(first.Disposed);
        Assert.True(second.Disposed);
    }

    [Fact]
    public void Dispose_on_array_disposes_each_item()
    {
        var item = new TrackingDisposable();
        new[] { item }.Dispose();
        Assert.True(item.Disposed);
    }

    [Fact]
    public void Clear_with_dispose_disposes_then_clears()
    {
        var first = new TrackingDisposable();
        var second = new TrackingDisposable();
        var items = new List<TrackingDisposable> { first, second };
        items.Clear(dispose: true);
        Assert.Empty(items);
        Assert.True(first.Disposed);
        Assert.True(second.Disposed);
    }

    [Fact]
    public void Clear_on_jagged_collection_disposes_arrays_when_requested()
    {
        var inner = new TrackingDisposable[1] { new() };
        var items = new List<TrackingDisposable[]> { inner };
        items.Clear(dispose: true);
        Assert.Empty(items);
        Assert.True(inner[0].Disposed);
    }

    [Fact]
    public void Null_collections_are_no_ops()
    {
        IEnumerable<TrackingDisposable>? items = null;
        items.Dispose();
        TrackingDisposable[]? array = null;
        array.Dispose();
    }

    private sealed class TrackingDisposable : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }
}
