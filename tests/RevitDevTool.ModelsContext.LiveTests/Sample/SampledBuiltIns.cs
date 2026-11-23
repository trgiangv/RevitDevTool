using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Parameters;
using Nice3point.Revit.Extensions;
using RevitDevTool.Tools.ModelsContext.Documents;
using RevitDevTool.Tools.ModelsContext.Filters;
using RevitDevTool.Tools.ModelsContext.Parameters;
using System.Diagnostics;
using System.Text;

namespace RevitDevTool.ModelsContext.LiveTests;

internal static class SampledBuiltIns
{
    private const int ListedFailures = 40;

    public static (int Failed, int Checked, string Report) Run()
    {
        var report = new StringBuilder();
        int failed = 0, total = 0;
        foreach (var document in Documents())
        {
            var (documentFailed, documentChecked) = Check(document, report);
            failed += documentFailed;
            total += documentChecked;
        }

        return (failed, total, report.ToString());
    }

    private static List<Document> Documents()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return SampleModel.All.Select(item => item.Document).Where(document => seen.Add(document.PathName)).ToList();
    }

    private static (int Failed, int Checked) Check(Document document, StringBuilder report)
    {
        var samples = Samples(document, out var excluded);
        var parser = RuleValueParser.For(document.GetUnits());
        var keys = samples.Keys.ToDictionary(parameter => parameter, Keys.BuiltIn);
        var items = samples
            .SelectMany(pair => pair.Value.Where(element => element.Category is not null).Select(element => (Parameter: pair.Key, Element: element, Text: ValueOf(document, element, keys[pair.Key]))))
            .Where(item => item.Text is not null)
            .ToList();
        var totals = new Dictionary<BuiltInParameter, int>();
        var passes = new Dictionary<BuiltInParameter, int>();
        var failures = new List<string>();
        long sweepMs = 0, planMs = 0;
        foreach (var category in items.GroupBy(item => item.Element.Category.Id.ToLong()).OrderBy(group => group.Key))
        {
            var set = ElementReader.CandidatesOf(document, [category.Key.ToElementId()]);
            var watch = Stopwatch.StartNew();
            var readers = category.Select(item => item.Parameter).Distinct().ToDictionary(parameter => parameter, parameter => new ValueReader(keys[parameter], set));
            foreach (var id in set.Ids)
            {
                if (document.GetElement(id.ToElementId()) is not { } candidate)
                {
                    continue;
                }

                foreach (var reader in readers.Values)
                {
                    reader.Add(candidate, id);
                }
            }

            sweepMs += watch.ElapsedMilliseconds;
            watch.Restart();
            foreach (var parameterGroup in category.GroupBy(item => item.Parameter))
            {
                var key = keys[parameterGroup.Key];
                var entry = readers[parameterGroup.Key].ToEntry();
                var resolved = ParameterLocator.Resolve(document, key, [category.Key.ToElementId()]);
                var compiler = new FilterCompiler(document.Title, _ => resolved, _ => entry.RawValues, parser, new TypeFilters(() => set.MembersIn(document)));
                var isNumber = resolved.Any(parameter => parameter.Storage == StorageType.Double);
                foreach (var valueGroup in parameterGroup.GroupBy(item => item.Text!))
                {
                    NumberRange? picked = isNumber && entry.Numbers.TryGetValue(valueGroup.Key, out var range) ? range : null;
                    var plan = compiler.Plan(FilterSet.All(new DevTools.ModelsContext.Filters.FilterRule(key, FilterOperator.Equal, valueGroup.Key, Picked: picked)));
                    var matched = new HashSet<long>(plan.Match(document, valueGroup.Select(item => item.Element.Id).ToList()));
                    foreach (var item in valueGroup)
                    {
                        totals[item.Parameter] = totals.GetValueOrDefault(item.Parameter) + 1;
                        if (matched.Contains(item.Element.Id.ToLong()))
                        {
                            passes[item.Parameter] = passes.GetValueOrDefault(item.Parameter) + 1;
                        }
                        else if (failures.Count < ListedFailures)
                        {
                            failures.Add($"  {item.Parameter} element {item.Element.Id} ({item.Element.Category.Name}) value '{item.Text}'");
                        }
                    }
                }
            }

            planMs += watch.ElapsedMilliseconds;
        }

        Evidence.Timing("sweep", sweepMs);
        Evidence.Timing("plans", planMs);
        var checkedSamples = totals.Values.Sum();
        var passed = passes.Values.Sum();
        var failed = checkedSamples - passed;
        report.AppendLine($"{document.Title}: {samples.Count} sampled built-ins, samples {checkedSamples}, passed {passed}, failed {failed}, excluded {string.Join(", ", excluded)}");
        foreach (var pair in totals.Where(pair => passes.GetValueOrDefault(pair.Key) != pair.Value).OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal))
        {
            report.AppendLine($"  {pair.Key}: {passes.GetValueOrDefault(pair.Key)} of {pair.Value}");
        }

        foreach (var failure in failures)
        {
            report.AppendLine(failure);
        }

        return (failed, checkedSamples);
    }
    private static Dictionary<BuiltInParameter, List<Element>> Samples(Document document, out SortedSet<string> excluded)
    {
        excluded = new SortedSet<string>(StringComparer.Ordinal);
        var filterableCategories = new HashSet<ElementId>(ParameterFilterUtilities.GetAllFilterableCategories());
        var filterable = new HashSet<ElementId>();
        foreach (var category in ElementReader.ModelCategoryIds(document).Where(filterableCategories.Contains))
        {
            filterable.UnionWith(ParameterFilterUtilities.GetFilterableParametersInCommon(document, [category]));
        }

        var sampled = new HashSet<long>();
        var samples = new Dictionary<BuiltInParameter, List<Element>>();
        foreach (var value in ElementReader.CollectAnyOption(document))
        {
            if (document.GetElement(value.ToElementId()) is not { } element)
            {
                continue;
            }

            var typeId = element.GetTypeId();
            if (!sampled.Add(typeId != ElementId.InvalidElementId ? typeId.ToLong() : element.Category?.Id.ToLong() ?? value))
            {
                continue;
            }

            var holders = document.GetElement(typeId) is { } type ? new[] { element, type } : [element];
            foreach (var holder in holders)
            {
                foreach (Parameter parameter in holder.Parameters)
                {
                    var id = parameter.Id.ToLong();
                    if (id >= 0 || filterable.Contains(parameter.Id) || parameter.StorageType == StorageType.None)
                    {
                        continue;
                    }

                    var builtIn = (BuiltInParameter)(int)id;
                    if (ParameterCatalogReader.Unfilterable.Contains(id))
                    {
                        excluded.Add(builtIn.ToString());
                        continue;
                    }

                    if (!samples.TryGetValue(builtIn, out var list))
                    {
                        samples.Add(builtIn, list = []);
                    }

                    if (!list.Contains(element))
                    {
                        list.Add(element);
                    }
                }
            }
        }

        return samples;
    }

    private static string? ValueOf(Document document, Element element, ParameterKey key)
    {
        return ParameterLocator.Find(element, key) is { } parameter ? ParameterValues.ValueText(document, parameter) : null;
    }
}
