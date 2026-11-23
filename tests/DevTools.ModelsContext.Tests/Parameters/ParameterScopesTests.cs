using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Parameters;

namespace DevTools.ModelsContext.Tests.Parameters;

[TestClass]
public sealed class ParameterScopesTests
{
    private const long Walls = 1;
    private const long Ducts = 2;

    private static readonly SourceId Arch = new("arch.rvt");
    private static readonly SourceId Mep = new("mep.rvt");

    private static readonly ParameterInfo Mark = Parameter("Mark", Walls, Ducts);
    private static readonly ParameterInfo FireRating = Parameter("Fire Rating", Walls);
    private static readonly ParameterInfo Insulation = Parameter("Insulation", Ducts);

    [TestMethod]
    public void SameScopeMergesOnce()
    {
        var (scopes, merges) = Create();
        var scope = new EffectiveScope([Arch], [Walls]);

        var first = scopes.For(scope);
        var second = scopes.For(new EffectiveScope([Arch], [Walls]));

        Assert.AreSame(first, second);
        Assert.AreEqual(1, merges());
        Assert.AreEqual(1, scopes.ScopeCount);
    }

    [TestMethod]
    public void OrderDoesNotSplitScope()
    {
        var (scopes, merges) = Create();

        scopes.For(new EffectiveScope([Arch, Mep], [Walls, Ducts]));
        scopes.For(new EffectiveScope([Mep, Arch], [Ducts, Walls]));

        Assert.AreEqual(1, merges());
    }

    [TestMethod]
    public void EachScopeMergesOnce()
    {
        var (scopes, merges) = Create();

        scopes.For(new EffectiveScope([Arch], null));
        scopes.For(new EffectiveScope([Mep], [Ducts]));
        scopes.For(new EffectiveScope([Arch], null));
        scopes.For(new EffectiveScope([Mep], [Ducts]));

        Assert.AreEqual(2, merges());
        Assert.AreEqual(2, scopes.ScopeCount);
    }

    [TestMethod]
    public void WholeScopeReusesAll()
    {
        var (scopes, merges) = Create();

        var whole = scopes.For(new EffectiveScope([Arch, Mep], null));

        Assert.AreSame(scopes.All, whole);
        Assert.AreEqual(1, merges());
    }

    [TestMethod]
    public void NewCatalogsClearCache()
    {
        var (scopes, merges) = Create();
        var scope = new EffectiveScope([Arch], null);
        scopes.For(scope);

        scopes.Set([new SourceCatalog(Arch, [Mark], [Walls])]);
        var parameters = scopes.For(scope);

        Assert.AreEqual(2, merges());
        CollectionAssert.AreEqual(new[] { Mark.Key }, parameters.Select(parameter => parameter.Key).ToArray());
    }

    [TestMethod]
    public void CategoryScopeFiltersParameters()
    {
        var (scopes, _) = Create();

        var parameters = scopes.For(new EffectiveScope([Arch, Mep], [Ducts]));

        CollectionAssert.AreEquivalent(new[] { Mark.Key, Insulation.Key },
            parameters.Select(parameter => parameter.Key).ToArray());
    }

    [TestMethod]
    public void EmptyScopeSkipsMerge()
    {
        var (scopes, merges) = Create();

        Assert.IsEmpty(scopes.For(new EffectiveScope([], null)));
        Assert.IsEmpty(scopes.For(new EffectiveScope([Arch], [])));
        Assert.AreEqual(0, merges());
    }

    [TestMethod]
    public void FindFallsBackToAll()
    {
        var (scopes, _) = Create();

        var found = scopes.Find(Insulation.Key, new EffectiveScope([Arch], null));

        Assert.AreEqual(Insulation.Key, found?.Key);
    }

    private static (ParameterScopes Scopes, Func<int> Merges) Create()
    {
        var count = 0;
        var scopes = new ParameterScopes(catalogs =>
        {
            count++;
            return ParameterCatalog.Merge(catalogs);
        });
        scopes.Set([
            new SourceCatalog(Arch, [Mark, FireRating], [Walls]),
            new SourceCatalog(Mep, [Mark, Insulation], [Ducts])
        ]);
        return (scopes, () => count);
    }

    private static ParameterInfo Parameter(string name, params long[] categories)
    {
        return new ParameterInfo(ParameterKey.Named(name, "text"), name, ValueKind.Text)
        {
            Categories = CategoryIdSet.Of(categories)
        };
    }
}
