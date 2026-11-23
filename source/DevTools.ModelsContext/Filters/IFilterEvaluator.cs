using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Parameters;

namespace DevTools.ModelsContext.Filters;

public interface IFilterEvaluator
{
    Task<IReadOnlyCollection<long>> EvaluateAsync(
        DocumentKey document,
        FilterDefinition filter,
        IReadOnlyCollection<long>? scope,
        CancellationToken cancellationToken);

    Task<ParameterValueCounts> CountValuesAsync(
        SourceId source,
        ParameterKey parameter,
        IReadOnlyCollection<long> groups,
        CancellationToken cancellationToken);

    Task<string?> FormatExampleAsync(DocumentKey model, string spec, CancellationToken cancellationToken);
}
