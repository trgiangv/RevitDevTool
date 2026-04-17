using System.Diagnostics;

namespace RevitDevTool.Core.Handlers;

/// <summary>Fire-and-forget request. Exceptions are traced, not propagated.</summary>
internal sealed class Request(Action<UIApplication> action) : IRevitRequest
{
    public void Execute(UIApplication uiApplication)
    {
        try
        {
            action(uiApplication);
        }
        catch (Exception exception)
        {
            Trace.TraceError("[RevitDispatcher] Request failed: {0}", exception);
        }
    }
}
