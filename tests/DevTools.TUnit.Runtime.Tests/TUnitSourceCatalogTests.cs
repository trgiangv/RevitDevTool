using TUnit.Core;

namespace DevTools.TUnit.Runtime.Tests;

public sealed class TUnitSourceCatalogTests
{
    [Fact]
    public void Retain_drops_test_entries_from_other_assemblies()
    {
        var current = typeof(TUnitSourceCatalogTests).Assembly;
        var foreignType = typeof(SourceRegistrar);
        var currentType = typeof(TUnitSourceCatalogTests);
        Sources.TestEntries[foreignType] = new StubSource(foreignType);
        Sources.TestEntries[currentType] = new StubSource(currentType);

        try
        {
            TUnitSourceCatalog.Retain(current);

            Assert.False(Sources.TestEntries.ContainsKey(foreignType));
            Assert.True(Sources.TestEntries.ContainsKey(currentType));
            Assert.All(Sources.TestEntries.Keys, type => Assert.Same(current, type.Assembly));
        }
        finally
        {
            Sources.TestEntries.TryRemove(foreignType, out _);
            Sources.TestEntries.TryRemove(currentType, out _);
        }
    }

    private sealed class StubSource : ITestEntrySource
    {
        public StubSource(Type classType) => ClassType = classType;

        public int Count => 0;
        public Type ClassType { get; }
        public string ClassName => ClassType.Name;

        public TestEntryFilterData GetFilterData(int index) =>
            throw new NotSupportedException();

        public IReadOnlyList<TestMetadata> Materialize(int index, string sessionId) =>
            [];
    }
}
