using RevitDevTool.CodeExecute.Interfaces;
using RevitDevTool.Controllers;

namespace RevitDevTool.CodeExecute.Providers.DotNet;

/// <summary>
/// Execution strategy for .NET commands.
/// </summary>
public sealed class DotNetExecutionStrategy(AddinItem addinItem) : IExecutionStrategy
{
    public void Execute()
    {
        var message = string.Empty;

        ExternalEventController.ActionEventHandler.Raise(_ =>
        {
            AddinExecutor.RunCommand(addinItem, AddinLoadHelper.ExternalCommandData, ref message, AddinLoadHelper.ElementSet);
        });
    }
}