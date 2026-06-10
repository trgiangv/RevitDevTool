namespace RevitDevTool.Core.Dispatchers;

/// <summary>Unit of work executed by <see cref="RevitDispatcher"/> on the Revit thread.</summary>
public interface IRevitRequest
{
    /// <summary>Executes the request in a valid Revit API context.</summary>
    void Execute(UIApplication uiApplication);

    /// <summary>
    /// Completes the request as failed without executing the callback.
    /// Used when the dispatcher cannot schedule the request.
    /// </summary>
    void Fail(Exception exception);
}
