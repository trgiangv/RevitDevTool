using System.Diagnostics;
namespace RevitDevTool.Core.Dispatchers;

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

    public void Fail(Exception exception)
    {
        Trace.TraceError("[RevitDispatcher] Request failed before execution: {0}", exception);
    }
}
