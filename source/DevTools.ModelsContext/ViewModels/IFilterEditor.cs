using DevTools.ModelsContext.Engine;
using DevTools.ModelsContext.Model;

namespace DevTools.ModelsContext.ViewModels;

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
