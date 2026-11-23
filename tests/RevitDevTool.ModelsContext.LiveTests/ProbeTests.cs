using DevTools.ModelsContext.Filters;
using System.Text.Json;

namespace RevitDevTool.ModelsContext.LiveTests;

[TestFixture]
public sealed class ProbeTests
{
    private const int SpreadSize = 12;

    [Test]
    public void WriteSampleProbe()
    {
        var documents = SampleModel.All.Select(document => new
        {
            label = document.Label,
            source = document.Source.ToString(),
            designOptions = new FilteredElementCollector(document.Document).OfClass(typeof(DesignOption)).Cast<DesignOption>()
                .Select(option => new { name = option.Name, primary = option.IsPrimary }),
            levels = new FilteredElementCollector(document.Document).OfClass(typeof(Level)).Select(level => level.Name).OrderBy(name => name),
            phases = document.Document.Phases.Cast<Phase>().Select(phase => phase.Name),
            shared = new FilteredElementCollector(document.Document).OfClass(typeof(SharedParameterElement)).Select(shared => shared.Name).OrderBy(name => name, StringComparer.Ordinal)
        }).ToList();
        var views = new FilteredElementCollector(SampleModel.Host).OfClass(typeof(View)).Cast<View>()
            .Where(view => !view.IsTemplate && view.ViewType is ViewType.ThreeD or ViewType.FloorPlan)
            .Select(view => $"{view.Name} [{view.ViewType}]");
        var spreads = Areas.Probes.Select(probe => new
        {
            docs = probe.Docs,
            category = probe.Category,
            parameter = probe.Parameter,
            values = Distribution(SampleModel.Of(probe.Docs)[0], probe.Category, Keys.Of(probe.Parameter))
        }).ToList();
        var probe = new
        {
            activeView = SampleModel.Application.ActiveUIDocument.ActiveView.Name,
            firstShared = Keys.Of("First shared").Name,
            documents,
            views,
            spreads
        };

        var path = Path.Combine(Evidence.Folder, "sample-probe.json");
        File.WriteAllText(path, JsonSerializer.Serialize(probe, CaseFiles.Options));
        Console.WriteLine(path);
    }

    private static Dictionary<string, int> Distribution(SampleDocument document, string category, ParameterKey key)
    {
        return Oracle.Candidates(document.Document, [Cases.Category(category)])
            .Select(element => Oracle.Read(element, key))
            .Select(read => read.Instance is { HasValue: true } instance ? instance : read.Type ?? read.Instance)
            .GroupBy(parameter => parameter is null ? "<undefined>" : parameter.AsValueString() ?? parameter.AsString() ?? "<empty>")
            .OrderByDescending(group => group.Count())
            .Take(SpreadSize)
            .ToDictionary(group => group.Key, group => group.Count());
    }
}
