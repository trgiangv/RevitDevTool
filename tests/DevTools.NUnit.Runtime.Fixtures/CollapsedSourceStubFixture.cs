using System.Collections;
using NUnit.Framework;

namespace DevTools.NUnit.Runtime.Fixtures;

// Testhost ExploreTests cannot expand this source → one NotRunnable leaf.
// UID is ITest.FullName (Class.Method), not Class("args").Method.

[TestFixtureSource(nameof(Cases))]
public sealed class CollapsedSourceStubFixture
{
    public CollapsedSourceStubFixture(string _)
    {
    }

    public static IEnumerable<TestFixtureData> Cases
    {
        get => throw new InvalidOperationException("source cannot expand in testhost");
    }

    [Test]
    public void Stub_leaf() => Assert.Pass();
}
