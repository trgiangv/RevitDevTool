using DevTools.ModelsContext.Documents;

namespace RevitDevTool.Tools.ModelsContext.Documents;

public interface IRevitModelEvents : IModelEvents
{
    bool IsActive { get; set; }

    void Subscribe(UIControlledApplication application);

    void Unsubscribe();
}
