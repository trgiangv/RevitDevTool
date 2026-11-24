using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB.Events;
using DevTools.ModelsContext.Diagnostics;
using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Visibility;
using Nice3point.Revit.Extensions;
using RevitDevTool.Tools.ModelsContext.Documents;
using RevitDevTool.Tools.ModelsContext.Pane;
using System.Globalization;
using RuleNode = DevTools.ModelsContext.Filters.FilterRule;

namespace RevitDevTool.Tools.ModelsContext.Visibility;

internal sealed record PreparedFilter(string Signature, List<ElementId> Categories, ElementFilter? Filter);

internal static class LinkViewFilters
{
    public const string Prefix = "Model Context ";

    private const string TransactionName = "Model Context";

    private static readonly Dictionary<(string Model, long View), Dictionary<string, string>> Registry = [];

    public static event Action<string>? Cleaned;

    public static bool IsSupported =>
#if REVIT2024_OR_GREATER
        true;
#else
        false;
#endif

    public static void Subscribe(ControlledApplication application)
    {
        application.DocumentSaving += OnSaving;
        application.DocumentSavingAs += OnSavingAs;
        application.DocumentSynchronizingWithCentral += OnSynchronizing;
        application.DocumentClosing += OnClosing;
    }

    public static void Unsubscribe(ControlledApplication application)
    {
        application.DocumentSaving -= OnSaving;
        application.DocumentSavingAs -= OnSavingAs;
        application.DocumentSynchronizingWithCentral -= OnSynchronizing;
        application.DocumentClosing -= OnClosing;
        Registry.Clear();
    }

    public static bool IsPanelName(string name)
    {
        return name.StartsWith(Prefix, StringComparison.Ordinal)
               && int.TryParse(name.Substring(Prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out _);
    }

    public static List<PreparedFilter> Prepare(Document model, View view, IEnumerable<LinkFilterStep> steps)
    {
        var list = steps.ToList();
        if (list.Count == 0)
        {
            return [];
        }

        if (!IsSupported)
        {
            throw new InvalidOperationException(VisibilityText.LinkFiltersUnavailable);
        }

        var filterable = new HashSet<long>(ParameterFilterUtilities.GetAllFilterableCategories().Select(id => id.ToLong()));
        return list.Select(step => Prepare(model, view, step, filterable)).ToList();
    }

    public static void Apply(
        Document model,
        View view,
        IReadOnlyList<PreparedFilter> prepared,
        Func<OverrideGraphicSettings, OverrideGraphicSettings> paint)
    {
        if (prepared.Count == 0)
        {
            return;
        }

        Sweep(model);
        var entries = EntriesOf(model, view);
        var applied = new HashSet<ElementId>();
        foreach (var item in prepared)
        {
            var filter = Find(model, entries, item.Signature) ?? Create(model, item, entries);
            if (!view.IsFilterApplied(filter.Id))
            {
                view.AddFilter(filter.Id);
            }

            view.SetFilterOverrides(filter.Id, paint(view.GetFilterOverrides(filter.Id)));
            applied.Add(filter.Id);
        }

        LowerOthers(model, view, applied);
    }

    private static void LowerOthers(Document model, View view, HashSet<ElementId> applied)
    {
        var others = view.GetOrderedFilters()
            .Where(id => !applied.Contains(id)
                         && model.GetElement(id) is ParameterFilterElement filter
                         && IsPanelName(filter.Name))
            .ToList();
        foreach (var id in others)
        {
            var overrides = view.GetFilterOverrides(id);
            var visible = view.GetFilterVisibility(id);
            var enabled = view.GetIsFilterEnabled(id);
            view.RemoveFilter(id);
            view.AddFilter(id);
            view.SetFilterOverrides(id, overrides);
            view.SetFilterVisibility(id, visible);
            view.SetIsFilterEnabled(id, enabled);
        }
    }

    public static bool HasAny(Document model, View view)
    {
        return Registry.TryGetValue(KeyOf(model, view), out var entries) && entries.Count > 0;
    }

    public static string? Remove(Document model, View view)
    {
        var key = KeyOf(model, view);
        if (!Registry.TryGetValue(key, out var entries))
        {
            return null;
        }

        Registry.Remove(key);
        return Delete(model, entries.Values);
    }

    public static bool HasAnyAnywhere => Registry.Count > 0;

    public static void Rename(string oldId, string newId)
    {
        foreach (var pair in Registry.Where(pair => pair.Key.Model == oldId).ToList())
        {
            Registry.Remove(pair.Key);
            Registry[(newId, pair.Key.View)] = pair.Value;
        }
    }

    public static void RemoveEverywhere(UIApplication application)
    {
        foreach (Document document in application.Application.Documents)
        {
            Clean(document);
        }
    }

    private static PreparedFilter Prepare(Document model, View view, LinkFilterStep step, HashSet<long> filterable)
    {
        var (instance, linkDocument) = LinkElements.RequireLink(model, step.Document);
        if (RevitVisibilityExplainer.DisplayModeOf(view, instance) is not null)
        {
            throw new InvalidOperationException(VisibilityText.NotByHostView);
        }

        if (step.Filter.Groups.Count == 0 || step.Filter.Groups.Any(category => !filterable.Contains(category)))
        {
            throw new InvalidOperationException(VisibilityText.NotFilterable);
        }

        var categories = step.Filter.Groups.Select(id => id.ToElementId()).ToList();
        var filter = new LinkFilterCompiler(model, linkDocument, categories).Compile(step.Filter.Root);
        if (filter is not null && !ParameterFilterElement.ElementFilterIsAcceptableForParameterFilterElement(model,
                new HashSet<ElementId>(categories), filter))
        {
            throw new InvalidOperationException(VisibilityText.LinkRuleNotNative);
        }

        return new PreparedFilter(SignatureOf(step.Filter), categories, filter);
    }

    private static ParameterFilterElement? Find(Document model, Dictionary<string, string> entries, string signature)
    {
        return entries.TryGetValue(signature, out var uniqueId)
               && model.GetElement(uniqueId) is ParameterFilterElement filter
               && IsPanelName(filter.Name)
            ? filter
            : null;
    }

    private static ParameterFilterElement Create(Document model, PreparedFilter item, Dictionary<string, string> entries)
    {
        var name = NextName(model);
        var filter = item.Filter is null
            ? ParameterFilterElement.Create(model, name, item.Categories)
            : ParameterFilterElement.Create(model, name, item.Categories, item.Filter);
        entries[item.Signature] = filter.UniqueId;
        return filter;
    }

    private static string NextName(Document model)
    {
        for (var index = 1; ; index++)
        {
            var name = Prefix + index.ToString(CultureInfo.InvariantCulture);
            if (ParameterFilterElement.IsNameUnique(model, name))
            {
                return name;
            }
        }
    }

    private static void Sweep(Document model)
    {
        var modelId = DocumentRegistry.ModelIdOf(model);
        var known = new HashSet<string>(Registry
            .Where(pair => pair.Key.Model == modelId)
            .SelectMany(pair => pair.Value.Values), StringComparer.Ordinal);
        var leftovers = model.CollectElements()
            .OfClass<ParameterFilterElement>()
            .Where(element => IsPanelName(element.Name) && !known.Contains(element.UniqueId))
            .Select(element => element.Id)
            .ToList();
        if (leftovers.Count > 0)
        {
            model.Delete(leftovers);
        }
    }

    private static string? Delete(Document model, IEnumerable<string> uniqueIds)
    {
        var failed = new List<string>();
        foreach (var uniqueId in uniqueIds)
        {
            if (model.GetElement(uniqueId) is not ParameterFilterElement filter || !IsPanelName(filter.Name))
            {
                continue;
            }

            try
            {
                model.Delete(filter.Id);
            }
            catch (Exception exception)
            {
                RevitProfiler.Logger.Swallowed(exception, "Delete view filter", filter.Name);
                failed.Add(filter.Name);
            }
        }

        return failed.Count == 0
            ? null
            : string.Format(CultureInfo.CurrentCulture, VisibilityText.NotDeleted, string.Join(", ", failed));
    }

    private static void Clean(Document model)
    {
        var modelId = DocumentRegistry.ModelIdOf(model);
        var keys = Registry.Keys.Where(key => key.Model == modelId).ToList();
        if (keys.Count == 0)
        {
            return;
        }

        var uniqueIds = keys.SelectMany(key => Registry[key].Values).ToList();
        foreach (var key in keys)
        {
            Registry.Remove(key);
        }

        try
        {
            if (model.IsModifiable)
            {
                Delete(model, uniqueIds);
                return;
            }

            using var transaction = new Transaction(model, TransactionName);
            transaction.Start();
            Delete(model, uniqueIds);
            transaction.Commit();
        }
        catch (Exception exception)
        {
            RevitProfiler.Logger.Swallowed(exception, "Clean view filters", model.Title);
        }
        finally
        {
            Cleaned?.Invoke(modelId);
        }
    }

    private static Dictionary<string, string> EntriesOf(Document model, View view)
    {
        var key = KeyOf(model, view);
        if (!Registry.TryGetValue(key, out var entries))
        {
            entries = new Dictionary<string, string>(StringComparer.Ordinal);
            Registry.Add(key, entries);
        }

        return entries;
    }

    private static (string Model, long View) KeyOf(Document model, View view)
    {
        return (DocumentRegistry.ModelIdOf(model), view.Id.ToLong());
    }

    private static string SignatureOf(FilterDefinition filter)
    {
        return string.Join(",", filter.Groups.OrderBy(id => id)) + "|" + Describe(filter.Root);
    }

    private static string Describe(FilterNode node)
    {
        return node switch
        {
            FilterSet set => set.Match + "(" + string.Join(";", set.Children.Select(Describe)) + ")",
            RuleNode rule => string.Join("|", rule.Parameter.BuiltInName ?? rule.Parameter.SharedId?.ToString() ?? rule.Parameter.Name,
                rule.Operator, rule.Value, rule.Picked?.ToString() ?? string.Empty),
            _ => node.ToString()
        };
    }

    private static void OnSaving(object? sender, DocumentSavingEventArgs e)
    {
        Clean(e.Document);
    }

    private static void OnSavingAs(object? sender, DocumentSavingAsEventArgs e)
    {
        Clean(e.Document);
    }

    private static void OnSynchronizing(object? sender, DocumentSynchronizingWithCentralEventArgs e)
    {
        Clean(e.Document);
    }

    private static void OnClosing(object? sender, DocumentClosingEventArgs e)
    {
        var modelId = DocumentRegistry.ModelIdOf(e.Document);
        foreach (var key in Registry.Keys.Where(key => key.Model == modelId).ToList())
        {
            Registry.Remove(key);
        }
    }
}
