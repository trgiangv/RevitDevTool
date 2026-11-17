using DevTools.ModelsContext.Model;
using Nice3point.Revit.Extensions;
using RevitDevTool.Tools.ModelsContext;
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
        var candidates = new Dictionary<long, CandidateSet>();
        int checkedSamples = 0, passed = 0;
        var failures = new List<string>();
        var failedParameters = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in samples.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal))
        {
            var key = Keys.BuiltIn(pair.Key);
            var groups = pair.Value
                .Select(element => (Element: element, Text: ValueOf(document, element, key)))
                .Where(item => item.Text is not null && item.Element.Category is not null)
                .GroupBy(item => (Category: item.Element.Category.Id.ToLong(), Text: item.Text!));
            int parameterPassed = 0, parameterTotal = 0;
            var entries = new Dictionary<long, ValueEntry>();
            foreach (var group in groups)
            {
                var set = CandidatesOf(document, group.Key.Category, candidates);
                var matched = Match(document, parser, key, group.Key.Category, group.Key.Text, set, group.Select(item => item.Element),
                    entries);
                foreach (var (element, text) in group)
                {
                    parameterTotal++;
                    if (matched.Contains(element.Id.ToLong()))
                    {
                        parameterPassed++;
                    }
                    else if (failures.Count < ListedFailures)
                    {
                        failures.Add($"  {pair.Key} element {element.Id} ({element.Category.Name}) value '{text}'");
                    }
                }
            }

            checkedSamples += parameterTotal;
            passed += parameterPassed;
            if (parameterPassed != parameterTotal)
            {
                failedParameters[pair.Key.ToString()] = $"{parameterPassed} of {parameterTotal}";
            }
        }

        var failed = checkedSamples - passed;
        report.AppendLine($"{document.Title}: {samples.Count} sampled built-ins, samples {checkedSamples}, passed {passed}, failed {failed}, excluded {string.Join(", ", excluded)}");
        foreach (var pair in failedParameters)
        {
            report.AppendLine($"  {pair.Key}: {pair.Value}");
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

    private static CandidateSet CandidatesOf(Document document, long category, Dictionary<long, CandidateSet> known)
    {
        if (!known.TryGetValue(category, out var set))
        {
            known.Add(category, set = ElementReader.CandidatesOf(document, [category.ToElementId()]));
        }

        return set;
    }

    private static HashSet<long> Match(
        Document document,
        RuleValueParser parser,
        ParameterKey key,
        long category,
        string text,
        CandidateSet candidates,
        IEnumerable<Element> elements,
        Dictionary<long, ValueEntry> entries)
    {
        List<ElementId> categories = [category.ToElementId()];
        var resolved = ParameterLocator.Resolve(document, key, categories);
        ValueEntry Entry() => entries.TryGetValue(category, out var known) ? known : entries[category] = Values(document, key, candidates);
        var compiler = new FilterCompiler(document.Title,
            _ => resolved,
            _ => Entry().RawValues,
            parser,
            new TypeFilters(() => candidates.MembersIn(document)));
        NumberRange? picked = resolved.Any(parameter => parameter.Storage == StorageType.Double)
                              && Entry().Numbers.TryGetValue(text, out var range)
            ? range
            : null;
        var plan = compiler.Plan(FilterSet.All(new DevTools.ModelsContext.Model.FilterRule(key, FilterOperator.Equal, text, Picked: picked)));
        return [.. plan.Match(document, elements.Select(element => element.Id).ToList())];
    }

    private static ValueEntry Values(Document document, ParameterKey key, CandidateSet candidates)
    {
        var reader = new ValueReader(key, candidates);
        var position = 0;
        while (position < candidates.Ids.Length)
        {
            position = reader.AddSlice(document, position);
        }

        return reader.ToEntry();
    }
}
