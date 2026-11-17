using DevTools.ModelsContext.Engine;
using DevTools.ModelsContext.Model;
using RevitDevTool.Tools.ModelsContext;

namespace RevitDevTool.ModelsContext.LiveTests;

[TestFixture]
public sealed class CatalogTests : RecordedTest
{
    private static readonly Dictionary<string, IReadOnlyList<ParameterInfo>> Catalogs = new(StringComparer.Ordinal);
    private static Adapter _adapter = null!;

    public static IEnumerable<TestCaseData> Rows => Areas.Rows(Areas.Catalog);

    [OneTimeSetUp]
    public void Open()
    {
        _adapter = new Adapter();
        Catalogs.Clear();
    }

    [TestCaseSource(nameof(Rows))]
    public void CatalogMatchesFacts(string id)
    {
        var item = Areas.Find(Areas.Catalog, id);
        Record(item, () =>
        {
            switch (item.Check)
            {
                case "Item":
                    Item(item);
                    break;
                case "Models":
                    Models(item);
                    break;
                case "ValueCount":
                    ValueCount(item);
                    break;
                case "SampledBuiltIns":
                    BuiltIns();
                    break;
                case "BuildTime":
                    BuildTime();
                    break;
                default:
                    Assert.Fail($"{id}: unknown check '{item.Check}' in catalog.json.");
                    break;
            }
        });
    }

    private static void Item(CatalogCase item)
    {
        var key = Keys.Of(item.Parameter);
        var found = Evidence.Time("panel", () => Catalog(item.Docs).Where(parameter => parameter.Key.Equals(key)).ToList());
        Evidence.Step($"Read the parameter catalog of {SampleModel.Of(item.Docs)[0].Document.Title} and looked up '{key.Name}'.");
        var info = found.FirstOrDefault();
        Evidence.Values(Facts(item), info is null ? "not listed" : $"{found.Count} item(s): {info.Kind}, {info.Origins}, {info.Bindings}");

        Assert.That(info, Is.Not.Null, $"{item.Parameter} is not in the catalog.");
        if (item.Count is { } count)
        {
            Assert.That(found, Has.Count.EqualTo(count), $"{item.Parameter} items in the catalog");
        }

        if (item.Kind is { } kind)
        {
            Assert.That(info!.Kind, Is.EqualTo(Enum.Parse(typeof(ValueKind), kind)));
        }

        foreach (var origin in item.Origins ?? [])
        {
            Assert.That(info!.Origins.HasFlag((ParameterOrigin)Enum.Parse(typeof(ParameterOrigin), origin)), Is.True, origin);
        }

        if (item.Bindings is { } bindings)
        {
            Assert.That(info!.Bindings, Is.EqualTo(Enum.Parse(typeof(ParameterBinding), bindings)));
        }

        foreach (var binding in item.BindingsInclude ?? [])
        {
            Assert.That(info!.Bindings.HasFlag((ParameterBinding)Enum.Parse(typeof(ParameterBinding), binding)), Is.True, binding);
        }
    }

    private static void Models(CatalogCase item)
    {
        var sources = new[] { SampleModel.Of("Host")[0].Source, SampleModel.Of("Arch")[0].Source };
        var merged = Evidence.Time("panel", () =>
        {
            var scopes = new ParameterScopes();
            scopes.Set([new SourceCatalog(sources[0], Catalog("Host"), []), new SourceCatalog(sources[1], Catalog("Arch"), [])],
                source => source.Label);
            return scopes.For(new EffectiveScope(sources, null));
        });
        var key = Keys.Of(item.Parameter);
        var info = merged.First(parameter => parameter.Key.Equals(key));
        Evidence.Step($"Merged the HVAC and Architectural catalogs and read the models that have '{key.Name}'.");
        Evidence.Values($"{item.Models} model(s)", $"{info.Models.Count} model(s): {string.Join(", ", info.Models)}");

        Assert.That(info.Models, Has.Count.EqualTo(item.Models));
    }

    private static void ValueCount(CatalogCase item)
    {
        var document = SampleModel.Of(item.Docs)[0];
        var key = Keys.Of(item.Parameter);
        var category = Cases.Category(item.Category);
        var oracle = Evidence.Time("oracle", () =>
        {
            var reads = Oracle.Candidates(document.Document, [category]).Select(element => Oracle.Read(element, key)).ToList();
            return (reads.Count, reads.Count(read => read.Instance is { HasValue: true } || read.Type is { HasValue: true }));
        });
        var counts = Evidence.Time("panel", () => Adapter.Run(_adapter.Evaluator.CountValuesAsync(document.Source, key, [category], CancellationToken.None)));
        Evidence.Step($"Counted the values of '{key.Name}' on {item.Category} in {document.Document.Title} with the panel's value tally and with plain reads.");
        Evidence.Values($"total {oracle.Item1}, with a value {oracle.Item2}", $"total {counts.Total}, with a value {counts.Values.Values.Sum()}");

        Assert.That(oracle, Is.EqualTo((item.Total, item.Values)), "oracle");
        Assert.That((counts.Total, counts.Values.Values.Sum()), Is.EqualTo(oracle), "panel");
    }

    private static void BuiltIns()
    {
        var (failed, total, report) = Evidence.Time("panel", SampledBuiltIns.Run);
        Evidence.Step("Sampled one element per type in every document, built an equals rule from each sampled built-in's own value and ran it through the panel's compiler.");
        foreach (var line in report.Split('\n').Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith(" ", StringComparison.Ordinal)))
        {
            Evidence.Step(line.Trim());
        }

        Evidence.Values($"{total} samples find themselves", $"{total - failed} found, {failed} failed");
        Assert.That(failed, Is.EqualTo(0), report);
    }

    private static void BuildTime()
    {
        var lines = new TraceLines();
        var previous = RevitProfiler.Logger;
        RevitProfiler.Use(lines);
        try
        {
            var cold = new Adapter();
            foreach (var docs in new[] { "Host", "Arch" })
            {
                var count = Evidence.Time("catalog " + docs, () =>
                    Adapter.Run(cold.Source.GetParameterCatalogAsync(SampleModel.Of(docs)[0].Source, CancellationToken.None)).Count);
                Evidence.Step($"{docs}: {count} parameters.");
            }
        }
        finally
        {
            RevitProfiler.Use(previous);
        }

        var catalogLines = lines.Lines.Where(line => line.StartsWith("Revit Catalog", StringComparison.Ordinal)).ToList();
        foreach (var line in catalogLines)
        {
            Evidence.Step(line);
        }

        Evidence.Values("Trace lines recorded for both documents", $"{catalogLines.Count} catalog lines");
        Assert.That(catalogLines, Is.Not.Empty);
    }

    private static string Facts(CatalogCase item)
    {
        return string.Join(", ", new[]
        {
            item.Count is { } count ? $"{count} item(s)" : null,
            item.Kind,
            item.Origins is { } origins ? string.Join("|", origins) : null,
            item.Bindings ?? (item.BindingsInclude is { } include ? "includes " + string.Join("|", include) : null)
        }.Where(part => part is not null));
    }

    private static IReadOnlyList<ParameterInfo> Catalog(string docs)
    {
        if (!Catalogs.TryGetValue(docs, out var catalog))
        {
            catalog = Adapter.Run(_adapter.Source.GetParameterCatalogAsync(SampleModel.Of(docs)[0].Source, CancellationToken.None));
            Catalogs.Add(docs, catalog);
        }

        return catalog;
    }
}
