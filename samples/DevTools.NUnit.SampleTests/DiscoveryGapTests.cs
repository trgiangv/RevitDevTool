using System.Collections;
using Autodesk.Revit.DB;
using NUnit.Framework;

namespace DevTools.NUnit.SampleTests;

// Local ExploreTests: [TestCaseSource] that news BoundingBoxXYZ at load → one NotRunnable leaf.
// UID run expands to in-host SetName leaves (Wide_box / Positive_octant).

[TestFixture]
public sealed class BoundingBoxCaseSourceTests
{
    [TestCaseSource(typeof(BoxCaseSource))]
    public void Box_source_has_positive_span(BoundingBoxXYZ box)
    {
        Assert.That(box.Max.X - box.Min.X, Is.GreaterThan(0.0));
        Assert.That(box.Max.Y - box.Min.Y, Is.GreaterThan(0.0));
        Assert.That(box.Max.Z - box.Min.Z, Is.GreaterThan(0.0));
    }
}

public sealed class BoxCaseSource : IEnumerable<TestCaseData>
{
    public IEnumerator<TestCaseData> GetEnumerator()
    {
        yield return new TestCaseData(BoundingBoxXyzSampleTests.Box(-12.3, 45.6, -7.8, 34.5, 67.8, 12.3))
            .SetName("Wide_box");
        yield return new TestCaseData(BoundingBoxXyzSampleTests.Box(10.5, 20.5, 30.5, 40.5, 50.5, 60.5))
            .SetName("Positive_octant");
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

// Local ExploreTests: [TestFixtureSource] that news BoundingBoxXYZ at load → one NotRunnable leaf.
// UID run expands to in-host BoundingBoxFixtureSourceTests("unit"|"neg").Span_is_one_on_each_axis.

[TestFixtureSource(nameof(Boxes))]
public sealed class BoundingBoxFixtureSourceTests
{
    private readonly BoundingBoxXYZ _box;

    public BoundingBoxFixtureSourceTests(BoundingBoxXYZ box) => _box = box;

    public static IEnumerable<TestFixtureData> Boxes
    {
        get
        {
            yield return new TestFixtureData(BoundingBoxXyzSampleTests.Box(0, 0, 0, 1, 1, 1))
                .SetArgDisplayNames("unit");
            yield return new TestFixtureData(BoundingBoxXyzSampleTests.Box(-1, -1, -1, 0, 0, 0))
                .SetArgDisplayNames("neg");
        }
    }

    [Test]
    public void Span_is_one_on_each_axis()
    {
        Assert.That(_box.Max.X - _box.Min.X, Is.EqualTo(1.0).Within(1e-9));
        Assert.That(_box.Max.Y - _box.Min.Y, Is.EqualTo(1.0).Within(1e-9));
        Assert.That(_box.Max.Z - _box.Min.Z, Is.EqualTo(1.0).Within(1e-9));
    }
}

// Not discovered: [TestFixture(typeof(XYZ))] / BoundingBoxXYZ. [TestFixture(typeof(int))] works.

[TestFixture(typeof(XYZ))]
[TestFixture(typeof(BoundingBoxXYZ))]
public sealed class GenericRevitTypeTests<T>
{
    [Test]
    public void Type_is_defined_in_revit_api()
    {
        Assert.That(typeof(T).Assembly.GetName().Name, Is.EqualTo("RevitAPI"));
    }
}

// NotRunnable leaf: FilteredElementCollector on a null Document. Does not poison the assembly.

[TestFixture]
public sealed class DocumentBoundCaseSourceTests
{
    public static IEnumerable<TestCaseData> WallTypeIdsFromNullDocument()
    {
        Document doc = null!;
        foreach (var id in new FilteredElementCollector(doc).OfClass(typeof(WallType)).ToElementIds())
            yield return new TestCaseData(id);
    }

    [TestCaseSource(nameof(WallTypeIdsFromNullDocument))]
    public void Wall_type_id_is_valid(ElementId id)
    {
        Assert.That(id, Is.Not.Null);
    }
}
