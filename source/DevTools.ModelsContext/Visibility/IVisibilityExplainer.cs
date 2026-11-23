using DevTools.ModelsContext.Documents;

namespace DevTools.ModelsContext.Visibility;

public interface IVisibilityExplainer
{
    Task<WhyHiddenResult> ExplainAsync(ElementRef element, CancellationToken cancellationToken);
}
