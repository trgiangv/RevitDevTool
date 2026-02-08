using RevitDevTool.CodeExecute.Interfaces;
using RevitDevTool.CodeExecute.Providers.DotNet.Models;
using RevitDevTool.Controllers;
using RevitDevTool.Utils;

namespace RevitDevTool.CodeExecute.Providers.DotNet;

/// <summary>
/// Execution strategy for .NET commands.
/// Wraps existing AddinExecutor logic.
/// </summary>
public sealed class DotNetExecutionStrategy : IExecutionStrategy
{
    private readonly AddinItem _addinItem;

    public DotNetExecutionStrategy(AddinItem addinItem)
    {
        _addinItem = addinItem;
    }

    public void Execute()
    {
        var message = string.Empty;

        ExternalEventController.ActionEventHandler.Raise(_ =>
        {
            AddinExecutor.RunCommand(_addinItem, AddinLoadHelper.ExternalCommandData, ref message, AddinLoadHelper.ElementSet);
        });
    }
}