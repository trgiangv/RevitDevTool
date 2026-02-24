using RevitDevTool.CodeExecute.Interfaces;
using RevitDevTool.Controllers;
namespace RevitDevTool.CodeExecute.Providers.Dotnet;

/// <summary>
/// Execution strategy for .NET commands.
/// </summary>
public sealed class AssemblyExecutionStrategy(AddinItem addinItem) : IExecutionStrategy
{
    public void Execute()
    {
        var message = string.Empty;

        ExternalEventController.ActionEventHandler.Raise(_ =>
        {
            AddinExecutor.RunCommand(addinItem, AddinCommandData.ExternalCommandData, ref message, AddinCommandData.ElementSet);
        });
    }
}