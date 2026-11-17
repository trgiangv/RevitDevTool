using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Tests.Shared;

namespace DevTools.ModelsContext.Tests;

[TestClass]
public sealed class ParameterModelTests
{
    [TestMethod]
    [DataRow(ParameterStorage.Integer, true, false, false, ValueKind.YesNo)]
    [DataRow(ParameterStorage.Double, false, true, false, ValueKind.Number)]
    [DataRow(ParameterStorage.Double, false, false, false, ValueKind.Number)]
    [DataRow(ParameterStorage.ElementId, false, false, false, ValueKind.Reference)]
    [DataRow(ParameterStorage.Integer, false, false, true, ValueKind.Enumerated)]
    [DataRow(ParameterStorage.Integer, false, false, false, ValueKind.Integer)]
    [DataRow(ParameterStorage.String, false, false, false, ValueKind.Text)]
    public void KindFollowsStorageAndSpec(
        ParameterStorage storage,
        bool isYesNo,
        bool isMeasurable,
        bool hasNamedValues,
        ValueKind expected)
    {
        Assert.AreEqual(expected, ValueKinds.Infer(storage, isYesNo, isMeasurable, hasNamedValues));
    }

    [TestMethod]
    public void TextHasAllOperators()
    {
        Assert.HasCount(16, FilterOperators.For(ValueKind.Text));
    }

    [TestMethod]
    [DataRow(ValueKind.Integer)]
    [DataRow(ValueKind.Number)]
    public void NumbersCompareAndHaveValue(ValueKind kind)
    {
        CollectionAssert.AreEquivalent(new[]
        {
            FilterOperator.Equal, FilterOperator.NotEqual, FilterOperator.Greater, FilterOperator.GreaterOrEqual,
            FilterOperator.Less, FilterOperator.LessOrEqual, FilterOperator.NotEmpty, FilterOperator.Empty,
            FilterOperator.Defined, FilterOperator.Undefined
        }, FilterOperators.For(kind).ToArray());
    }

    [TestMethod]
    [DataRow(ValueKind.YesNo)]
    [DataRow(ValueKind.Reference)]
    [DataRow(ValueKind.Enumerated)]
    public void PickedKindsOnlyEqual(ValueKind kind)
    {
        CollectionAssert.AreEquivalent(new[]
        {
            FilterOperator.Equal, FilterOperator.NotEqual, FilterOperator.NotEmpty, FilterOperator.Empty,
            FilterOperator.Defined, FilterOperator.Undefined
        }, FilterOperators.For(kind).ToArray());
    }

    [TestMethod]
    public void BuiltInKeyIgnoresLabel()
    {
        Assert.AreEqual(ParameterKey.BuiltIn("Mark", "ALL_MODEL_MARK"), ParameterKey.BuiltIn("Marque", "ALL_MODEL_MARK"));
    }

    [TestMethod]
    public void SameNameTwoTypesTwoItems()
    {
        var merged = ParameterCatalog.Merge([
            [Item(FakeParameters.TextCode, ParameterOrigin.Project)],
            [Item(FakeParameters.IntegerCode, ParameterOrigin.Project)]
        ]);

        Assert.HasCount(2, merged);
    }

    [TestMethod]
    public void FamilyAndProjectMergeTags()
    {
        var merged = ParameterCatalog.Merge([
            [Item(FakeParameters.Width, ParameterOrigin.Project, ParameterBinding.Instance) with { Models = ["A"] }],
            [Item(FakeParameters.Width, ParameterOrigin.Family, ParameterBinding.Type) with { Models = ["B"] }],
            [Item(FakeParameters.Mark, ParameterOrigin.BuiltIn) with { Models = ["C"] }]
        ]);

        var width = merged.Single(parameter => parameter.Key == FakeParameters.Width);
        Assert.AreEqual(ParameterOrigin.Project | ParameterOrigin.Family, width.Origins);
        Assert.AreEqual(ParameterBinding.Instance | ParameterBinding.Type, width.Bindings);
        CollectionAssert.AreEqual(new[] { "A", "B" }, width.Models.ToArray());
    }

    [TestMethod]
    public void EnumeratedWinsOverInteger()
    {
        var key = ParameterKey.BuiltIn("Function", "FUNCTION_PARAM");
        var merged = ParameterCatalog.Merge([
            [new ParameterInfo(key, "Function", ValueKind.Integer, ParameterStorage.Integer)],
            [new ParameterInfo(key, "Function", ValueKind.Enumerated, ParameterStorage.Integer)]
        ]);

        Assert.AreEqual(ValueKind.Enumerated, merged.Single().Kind);
    }

    [TestMethod]
    public void MergeCountsEveryDocument()
    {
        var catalogs = FakeCatalogs.Documents(4);

        var merged = ParameterCatalog.Merge(catalogs);

        Assert.AreEqual(catalogs.SelectMany(catalog => catalog).Select(item => item.Key).Distinct().Count(), merged.Count);
        foreach (var item in merged)
        {
            var owners = catalogs.Where(catalog => catalog.Any(other => other.Key == item.Key)).ToList();
            Assert.AreEqual(owners.SelectMany(catalog => catalog).Where(other => other.Key == item.Key)
                .Aggregate(ParameterOrigin.None, (origins, other) => origins | other.Origins), item.Origins);
        }
    }

    [TestMethod]
    public void MergeUnionsCategories()
    {
        var catalogs = FakeCatalogs.Documents(4);

        var merged = ParameterCatalog.Merge(catalogs);

        foreach (var item in merged)
        {
            var expected = catalogs.SelectMany(catalog => catalog).Where(other => other.Key == item.Key)
                .SelectMany(other => other.Categories).Distinct().OrderBy(id => id).ToList();
            CollectionAssert.AreEqual(expected, item.Categories.ToList());
        }
    }

    private static ParameterInfo Item(
        ParameterKey key,
        ParameterOrigin origin,
        ParameterBinding binding = ParameterBinding.Instance)
    {
        return new ParameterInfo(key, key.Name, ValueKind.Text, ParameterStorage.String, key.DataType, null, origin,
            binding);
    }
}
