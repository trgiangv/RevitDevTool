using DevTools.ModelsContext.Documents;

namespace DevTools.ModelsContext.Visibility;

public interface IVisibilityExplainer
{
    Task<WhyHidden> ExplainAsync(ElementRef element, CancellationToken cancellationToken);
}
