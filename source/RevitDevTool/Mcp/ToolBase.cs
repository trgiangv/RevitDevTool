using Autodesk.Revit.UI;
using RevitDevTool.Controllers;
using RevitDevTool.Core;

namespace RevitDevTool.Mcp;

internal abstract class ToolBase
{
    public abstract string ToolName { get; }

    public IDictionary<string, object?> ProcessRequest()
    {
        var handlerTask = ExternalEventController.AsyncGenericEventHandler<IDictionary<string, object?>>();
        var handler = handlerTask.GetAwaiter().GetResult();
        return handler.RaiseAsync(() => Execute(RevitContext.UiApplication)).GetAwaiter().GetResult();
    }

    protected abstract IDictionary<string, object?> Execute(UIApplication uiApp);
}
