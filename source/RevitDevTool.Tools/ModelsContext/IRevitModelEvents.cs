using DevTools.ModelsContext.Ports;

namespace RevitDevTool.Tools.ModelsContext;

public interface IRevitModelEvents : IModelEvents
{
    bool IsActive { get; set; }

    void Subscribe(UIControlledApplication application);

    void Unsubscribe();
}
