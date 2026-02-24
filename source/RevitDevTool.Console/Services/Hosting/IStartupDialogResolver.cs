namespace RevitDevTool.Console.Services.Hosting;

/// <summary>
/// Monitors startup dialogs for a launched Revit process and dismisses whitelisted prompts.
/// </summary>
public interface IStartupDialogResolver
{
    Task RunAsync(int processId, string hostVersion, CancellationToken ct = default);
}
