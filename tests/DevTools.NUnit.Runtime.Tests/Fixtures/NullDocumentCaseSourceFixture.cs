using System.Collections;
using NUnit.Framework;

namespace DevTools.NUnit.Runtime.Tests.Fixtures;

[TestFixture]
public sealed class NullDocumentCaseSourceFixture
{
    // Mirrors a Revit source that dereferences a Document which is null outside a host.
    public static IEnumerable Cases()
    {
        Document? document = null;
        ArgumentNullException.ThrowIfNull(document);
        yield return document.Id;
    }

    [TestCaseSource(nameof(Cases))]
    public void Leaf(int value) => Assert.That(value, Is.GreaterThan(0));

    private sealed class Document
    {
        public int Id => 1;
    }
}
