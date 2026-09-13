using System.Reflection;
using System.Runtime.Loader;
using TUnit.Core;

namespace DevTools.TUnit.Runtime.Tests;

[TestClass]
[DoNotParallelize]
public sealed class TUnitSourceCatalogTests
{
    [TestMethod]
    public void Retain_hides_foreign_entries_from_the_live_catalog()
    {
        var current = typeof(TUnitSourceCatalogTests).Assembly;
        var foreignType = typeof(SourceRegistrar);
        var currentType = typeof(TUnitSourceCatalogTests);
        Sources.TestEntries[foreignType] = new StubSource(foreignType);
        Sources.TestEntries[currentType] = new StubSource(currentType);

        try
        {
            TUnitSourceCatalog.Retain(current);

            Assert.IsFalse(Sources.TestEntries.ContainsKey(foreignType));
            Assert.IsTrue(Sources.TestEntries.ContainsKey(currentType));
            foreach (var type in Sources.TestEntries.Keys)
                Assert.AreSame(current, type.Assembly);
        }
        finally
        {
            Clear(foreignType, currentType);
        }
    }

    [TestMethod]
    public void Retain_restores_parked_entries_when_switching_back_to_a_prior_assembly()
    {
        var current = typeof(TUnitSourceCatalogTests).Assembly;
        var foreignType = typeof(SourceRegistrar);
        var currentType = typeof(TUnitSourceCatalogTests);
        var currentSource = new StubSource(currentType);
        var foreignSource = new StubSource(foreignType);
        Sources.TestEntries[foreignType] = foreignSource;
        Sources.TestEntries[currentType] = currentSource;

        try
        {
            TUnitSourceCatalog.Retain(current);
            TUnitSourceCatalog.Retain(foreignType.Assembly);

            Assert.IsTrue(Sources.TestEntries.ContainsKey(foreignType));
            Assert.IsFalse(Sources.TestEntries.ContainsKey(currentType));

            TUnitSourceCatalog.Retain(current);

            Assert.IsTrue(Sources.TestEntries.ContainsKey(currentType));
            Assert.AreSame(currentSource, Sources.TestEntries[currentType]);
            Assert.IsFalse(Sources.TestEntries.ContainsKey(foreignType));
        }
        finally
        {
            Clear(foreignType, currentType);
        }
    }

    [TestMethod]
    public void Retain_restores_from_process_hold_when_a_second_runtime_copy_parked_the_entries()
    {
        var current = typeof(TUnitSourceCatalogTests).Assembly;
        var foreignType = typeof(SourceRegistrar);
        var currentType = typeof(TUnitSourceCatalogTests);
        var currentSource = new StubSource(currentType);
        var foreignSource = new StubSource(foreignType);
        Sources.TestEntries[foreignType] = foreignSource;
        Sources.TestEntries[currentType] = currentSource;

        var runtimePath = typeof(TUnitSourceCatalog).Assembly.Location;
        Assert.IsFalse(string.IsNullOrWhiteSpace(runtimePath));
        var copyDirectory = Directory.CreateTempSubdirectory();
        var copyPath = Path.Combine(copyDirectory.FullName, Path.GetFileName(runtimePath));
        File.Copy(runtimePath, copyPath);
        var alc = new AssemblyLoadContext("tunit-catalog-r2-" + Guid.NewGuid().ToString("N"), isCollectible: true);
        alc.Resolving += (_, name) =>
        {
            if (string.Equals(name.Name, "DevTools.TUnit.Runtime", StringComparison.Ordinal))
                return null;

            return AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly =>
                !assembly.IsDynamic
                && string.Equals(assembly.GetName().Name, name.Name, StringComparison.Ordinal));
        };

        try
        {
            var runtimeCopy = alc.LoadFromAssemblyPath(copyPath);
            Assert.AreNotSame(typeof(TUnitSourceCatalog).Assembly, runtimeCopy);
            InvokeRetain(runtimeCopy, current);

            Assert.IsFalse(Sources.TestEntries.ContainsKey(foreignType));
            Assert.IsTrue(Sources.TestEntries.ContainsKey(currentType));

            TUnitSourceCatalog.Retain(foreignType.Assembly);

            Assert.IsTrue(Sources.TestEntries.ContainsKey(foreignType));
            Assert.AreSame(foreignSource, Sources.TestEntries[foreignType]);
            Assert.IsFalse(Sources.TestEntries.ContainsKey(currentType));
        }
        finally
        {
            Clear(foreignType, currentType);
            alc.Unload();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            try
            {
                Directory.Delete(copyDirectory.FullName, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private static void InvokeRetain(Assembly runtimeCopy, Assembly testAssembly)
    {
        var catalog = runtimeCopy.GetType("DevTools.TUnit.Runtime.TUnitSourceCatalog", throwOnError: true)!;
        var retain = catalog.GetMethod("Retain", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("TUnitSourceCatalog.Retain was not found on the Runtime copy.");
        retain.Invoke(null, [testAssembly]);
    }

    private static void Clear(Type foreignType, Type currentType)
    {
        TUnitSourceCatalog.Retain(foreignType.Assembly);
        Sources.TestEntries.TryRemove(foreignType, out _);
        Sources.TestEntries.TryRemove(currentType, out _);
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
