namespace RevitDevTool.Core.Handlers;

/// <summary>Unit of work executed by <see cref="RevitDispatcher"/> on the Revit thread.</summary>
internal interface IRevitRequest
{
    void Execute(UIApplication uiApplication);
}
