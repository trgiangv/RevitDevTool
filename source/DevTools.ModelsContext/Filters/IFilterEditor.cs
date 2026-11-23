using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Parameters;

namespace DevTools.ModelsContext.Filters;

internal interface IFilterEditor
{
    ParameterScopes Parameters { get; }

    EffectiveScope RootScope { get; }

    IReadOnlyList<long> AllCategories { get; }

    bool IsKnown(SourceId source);

    string DocumentLabel(SourceId source);

    string CategoryLabel(long categoryId);

    void OnEdited();

    Task LoadValuesAsync(RuleViewModel rule);

    Task LoadExampleAsync(RuleViewModel rule);
}
