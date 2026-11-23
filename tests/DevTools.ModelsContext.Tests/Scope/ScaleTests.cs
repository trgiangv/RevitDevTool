using DevTools.ModelsContext.Scope;
using DevTools.ModelsContext.Tests.Shared;
using System.Diagnostics;
using System.Globalization;

namespace DevTools.ModelsContext.Tests.Scope;

[TestClass]
public sealed class ScaleTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void MillionElementsBuildAndAggregate()
    {
        var generate = Stopwatch.StartNew();
        var data = FakeModelGenerator.Generate(FakeModelOptions.Million);
        generate.Stop();

        var build = Stopwatch.StartNew();
        var snapshots = data.Sources.Values.ToDictionary(source => source.Id, source => FakeModelGenerator.BuildSnapshot(source));
        build.Stop();

        var aggregate = Stopwatch.StartNew();
        var merged = SnapshotAggregator.MergeGroups(data.Documents.Select(document =>
            SnapshotAggregator.GroupsOf(snapshots[document.Source], snapshots[document.Source].Counts)));
        var recount = snapshots.Values.Sum(snapshot => SnapshotAggregator.Aggregate(snapshot).Count);
        aggregate.Stop();

        var elements = snapshots.Values.Sum(snapshot => snapshot.ElementCount);
        var bytes = snapshots.Values.Sum(snapshot => snapshot.EstimatedBytes);
        var perMillion = bytes * 1_000_000.0 / elements / (1024 * 1024);
        Log("Generated {0:N0} elements in {1:N0} ms", elements, generate.ElapsedMilliseconds);
        Log("Built {0} snapshots in {1:N0} ms", snapshots.Count, build.ElapsedMilliseconds);
        Log("Aggregated {0} merged groups in {1:N0} ms", merged.Count, aggregate.ElapsedMilliseconds);
        Log("Snapshot memory {0:N1} MB total, {1:N1} MB per million elements", bytes / (1024.0 * 1024), perMillion);

        Assert.AreEqual(5, snapshots.Count);
        Assert.AreEqual(1_000_000, elements);
        Assert.AreEqual(elements, recount);
        Assert.IsLessThan(64.0, perMillion);
    }

    [TestMethod]
    [DataRow(26_000)]
    [DataRow(50_000)]
    public async Task ExpandAllStaysFast(int elements)
    {
        var data = FakeModelGenerator.Generate(new FakeModelOptions { CurrentElements = elements, LinkElements = 2_000 });
        using var fixture = await PanelFixture.OpenAsync(data);

        var expand = Stopwatch.StartNew();
        await fixture.Explorer.ExpandTreeAsync();
        expand.Stop();
        var rows = fixture.Explorer.Rows.Count;
        var collapse = Stopwatch.StartNew();
        fixture.Explorer.CollapseTree();
        collapse.Stop();

        Log("Expand all on {0:N0} elements: {1:N0} rows in {2:N0} ms, collapse all in {3:N0} ms",
            elements, rows, expand.ElapsedMilliseconds, collapse.ElapsedMilliseconds);
        Assert.IsLessThan(1_000L, expand.ElapsedMilliseconds);
    }

    private void Log(string format, params object[] values)
    {
        var line = string.Format(CultureInfo.InvariantCulture, format, values);
        TestContext.WriteLine(line);
        Console.WriteLine(line);
    }
}
