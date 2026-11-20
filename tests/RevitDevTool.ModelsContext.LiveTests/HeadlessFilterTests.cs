using DevTools.ModelsContext.Engine;
using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Ports;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RevitDevTool.ModelsContext.LiveTests;

internal sealed class HeadlessData
{
    public List<HeadlessEntry> Headless { get; set; } = [];
}

internal sealed class HeadlessEntry
{
    public string Id { get; set; } = string.Empty;

    public string Case { get; set; } = string.Empty;

    public JsonElement Stored { get; set; }
}

[TestFixture]
public sealed class HeadlessFilterTests : RecordedTest
{
    private static readonly Lazy<List<HeadlessEntry>> LazyEntries = new(() => CaseFiles.Read<HeadlessData>("headless-filters.json").Headless);
    private static Adapter _adapter = null!;

    public static IEnumerable<TestCaseData> Entries => LazyEntries.Value.Select(entry => new TestCaseData(entry.Id).SetArgDisplayNames(entry.Id));

    [OneTimeSetUp]
    public void Open()
    {
        _adapter = new Adapter();
    }

    [TestCaseSource(nameof(Entries))]
    public void JsonMatchesPanel(string id)
    {
        var entry = LazyEntries.Value.First(candidate => candidate.Id == id);
        var item = CaseFiles.Filter(entry.Case);
        Record(Item(entry, item), () =>
        {
            var (documents, definition) = Load(entry, item);
            Evidence.Step("Read the stored custom-filter JSON (the format CustomFilterStore reads and writes) and mapped it with StoredFilters.Read to a FilterDefinition.");

            var known = SampleModel.KnownSources;
            IFilterEvaluator evaluator = _adapter.Evaluator;
            var headless = Evidence.Time("headless", () => Adapter.Run(RunAsync(evaluator, known, definition, documents)).Count);
            Evidence.Step("Headless: ran FilterScopes.ForDocument and IFilterEvaluator.EvaluateAsync per document through the core ports, with no panel view model.");

            var again = StoredFilters.Read(StoredFilters.Write([new CustomFilter("round trip", definition)]))[0].Definition;
            var roundTrip = Adapter.Run(RunAsync(evaluator, known, again, documents)).Count;
            Evidence.Step("Wrote the definition back with StoredFilters.Write, read it again and ran it.");

            var scope = Cases.Scope(item);
            var oracle = Evidence.Time("oracle", () => Oracle.Count(definition, documents, scope));
            var panel = Evidence.Time("panel", () => _adapter.Count(Cases.Definition(item), documents, scope));
            Evidence.Values(oracle, headless);

            Assert.That(headless, Is.EqualTo(oracle), $"{id}: headless {headless}, oracle {oracle}.");
            Assert.That(headless, Is.EqualTo(panel), $"{id}: headless {headless}, panel (case {item.Id}) {panel}.");
            Assert.That(roundTrip, Is.EqualTo(headless), $"{id}: the stored form written back counts {roundTrip}, not {headless}.");
        });
    }

    [Test]
    public void ConcurrentRunsStayCorrect()
    {
        var item = new CaseItem
        {
            Id = "MC-07",
            Description = "Three headless runs started before any finishes, on the same documents and caches: MC-01, MC-04 and MC-01 again",
            Expected = JsonSerializer.SerializeToElement("each run equals its oracle"),
            ManualSteps =
            [
                "Open the sample as for TX-01 and GS-02.",
                "Run the stored JSON of MC-01 and MC-04 (headless-filters.json) through the core ports without the panel, started one after the other without waiting.",
                "Each result must equal the count of its filter case (TX-01 and GS-02)."
            ]
        };
        Record(item, () =>
        {
            var first = Entry("MC-01");
            var second = Entry("MC-04");
            var known = SampleModel.KnownSources;
            IFilterEvaluator evaluator = _adapter.Evaluator;
            var runs = Evidence.Time("runs", () =>
            {
                var a = RunAsync(evaluator, known, first.Definition, first.Documents);
                var b = RunAsync(evaluator, known, second.Definition, second.Documents);
                var c = RunAsync(evaluator, known, first.Definition, first.Documents);
                Adapter.Run(Task.WhenAll(a, b, c));
                return (A: a.GetAwaiter().GetResult(), B: b.GetAwaiter().GetResult(), C: c.GetAwaiter().GetResult());
            });
            Evidence.Step("Started three evaluations on the same Adapter caches before waiting for any; none threw.");

            var oracleFirst = Oracle.Count(first.Definition, first.Documents, ScopeKind.WholeModel);
            var oracleSecond = Oracle.Count(second.Definition, second.Documents, ScopeKind.WholeModel);
            Evidence.Values($"{oracleFirst}, {oracleSecond}, {oracleFirst}", $"{runs.A.Count}, {runs.B.Count}, {runs.C.Count}");

            Assert.That(runs.A.Count, Is.EqualTo(oracleFirst));
            Assert.That(runs.B.Count, Is.EqualTo(oracleSecond));
            Assert.That(runs.C.Count, Is.EqualTo(oracleFirst));
            Assert.That(runs.A.Select(match => match.Id).ToHashSet().SetEquals(runs.C.Select(match => match.Id)), Is.True, "the same filter gave two different sets");
        });
    }

    private static (IReadOnlyList<SampleDocument> Documents, FilterDefinition Definition) Load(HeadlessEntry entry, FilterCaseData item)
    {
        Assert.That(Cases.Scope(item), Is.EqualTo(ScopeKind.WholeModel), $"{entry.Id}: headless cases run on the whole model.");
        var stored = JsonSerializer.Deserialize<ModelContextFilters>(Resolve(entry.Stored.GetRawText()))
                     ?? throw new InvalidDataException($"{entry.Id}: the stored filter is empty.");
        var filters = StoredFilters.Read(stored);
        Assert.That(filters, Has.Count.EqualTo(1), $"{entry.Id}: one stored filter expected.");
        return (SampleModel.Of(item.Docs), filters[0].Definition);
    }

    private static (IReadOnlyList<SampleDocument> Documents, FilterDefinition Definition) Entry(string id)
    {
        var entry = LazyEntries.Value.First(candidate => candidate.Id == id);
        return Load(entry, CaseFiles.Filter(entry.Case));
    }

    private static CaseItem Item(HeadlessEntry entry, FilterCaseData item)
    {
        return new CaseItem
        {
            Id = entry.Id,
            Description = $"{item.Id} as stored JSON through the core ports, no panel: {item.Description}",
            Expected = item.Expected.Clone(),
            ManualSteps = item.ManualSteps
        };
    }

    private static string Resolve(string json)
    {
        json = json.Replace("\"$host\"", JsonSerializer.Serialize(SampleModel.Current.Source.Value))
            .Replace("\"$arch\"", JsonSerializer.Serialize(SampleModel.Architectural.Source.Value));
        return Regex.Replace(json, "\"\\$(OST_[A-Za-z0-9_]+)\"", match =>
            ((long)(BuiltInCategory)Enum.Parse(typeof(BuiltInCategory), match.Groups[1].Value)).ToString(CultureInfo.InvariantCulture));
    }

    private static async Task<List<ElementRef>> RunAsync(IFilterEvaluator evaluator, ICollection<SourceId> known, FilterDefinition definition,
        IReadOnlyList<SampleDocument> documents)
    {
        var result = new List<ElementRef>();
        foreach (var document in documents)
        {
            if (FilterScopes.ForDocument(definition.Root, document.Source, known) is not { } root)
            {
                continue;
            }

            var ids = await evaluator.EvaluateAsync(document.Key, definition with { Root = root }, null, CancellationToken.None);
            result.AddRange(ids.Select(id => new ElementRef(document.Key, id)));
        }

        return result;
    }
}
