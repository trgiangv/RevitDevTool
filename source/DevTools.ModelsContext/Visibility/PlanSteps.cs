using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Filters;

namespace DevTools.ModelsContext.Visibility;

public abstract record PlanStep;

public abstract record DocumentStep(DocumentKey Document) : PlanStep;

public sealed record CategoryStep(long CategoryId, string Name) : PlanStep;

public sealed record LinkInstanceStep(DocumentKey Document) : DocumentStep(Document);

public sealed record ElementsStep(DocumentKey Document, IReadOnlyList<long> Ids) : DocumentStep(Document);

public sealed record LinkElementsStep(DocumentKey Document, IReadOnlyList<long> Ids) : DocumentStep(Document);

public sealed record LinkIsolateStep(DocumentKey Document, IReadOnlyList<long> KeptIds) : DocumentStep(Document);

public sealed record LinkFilterStep(DocumentKey Document, IReadOnlyList<long> Ids, FilterDefinition Filter)
    : DocumentStep(Document);
