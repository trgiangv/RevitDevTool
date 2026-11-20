using DevTools.ModelsContext.Engine;
using DevTools.ModelsContext.Model;
using RevitDevTool.Tools.ModelsContext;

namespace RevitDevTool.ModelsContext.LiveTests;

internal sealed class Adapter
{
    private static readonly TimeSpan CallLimit = TimeSpan.FromSeconds(90);

    public ParameterCache Cache { get; } = new();

    public ScopeCache Scopes { get; } = new();

    public RevitFilterEvaluator Evaluator => new(Cache);

    public RevitModelSource Source => new(Scopes, Cache);

    public RevitFilterCatalog HostFilters { get; } = new();

    public int Count(FilterDefinition definition, IReadOnlyList<SampleDocument> documents, ScopeKind scope)
    {
        return Matches(definition, documents, scope).Count;
    }

    public List<ElementRef> Matches(FilterDefinition definition, IReadOnlyList<SampleDocument> documents, ScopeKind scope)
    {
        return Matches(definition, documents, scope, SampleModel.KnownSources);
    }

    public List<ElementRef> Matches(FilterDefinition definition, IReadOnlyList<SampleDocument> documents, ScopeKind scope,
        ICollection<SourceId> known)
    {
        var matches = new List<ElementRef>();
        foreach (var document in documents)
        {
            if (FilterScopes.ForDocument(definition.Root, document.Source, known) is not { } root)
            {
                continue;
            }

            var ids = scope == ScopeKind.WholeModel ? null : Run(Source.CollectScopeAsync(document.Key, scope, CancellationToken.None));
            matches.AddRange(Run(Evaluator.EvaluateAsync(document.Key, definition with { Root = root }, ids, CancellationToken.None))
                .Select(id => new ElementRef(document.Key, id)));
        }

        return matches;
    }

    public static void Run(Task task)
    {
        if (!task.Wait(CallLimit))
        {
            Assert.Fail($"An adapter call did not finish within {CallLimit.TotalSeconds} s; it waited for the Revit thread the test holds.");
        }

        task.GetAwaiter().GetResult();
    }

    public static T Run<T>(Task<T> task)
    {
        if (!task.Wait(CallLimit))
        {
            Assert.Fail($"An adapter call did not finish within {CallLimit.TotalSeconds} s; it waited for the Revit thread the test holds.");
        }

        return task.GetAwaiter().GetResult();
    }
}
