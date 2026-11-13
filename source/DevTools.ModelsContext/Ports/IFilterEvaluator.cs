using DevTools.ModelsContext.Model;

namespace DevTools.ModelsContext.Ports;

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
}
