using System.Diagnostics;
using System.IO;
using RevitDevTool.CodeExecute.Interfaces;
using RevitDevTool.Controllers;
using RevitDevTool.Utils;
using RevitDevTool.View;
using RevitDevTool.ViewModel;

namespace RevitDevTool.CodeExecute.Providers.Python;

/// <summary>
/// Execution strategy for Python scripts.
/// Handles dependency resolution and execution orchestration directly.
/// </summary>
public sealed class PythonExecutionStrategy(string scriptPath, string rootPath) : IExecutionStrategy
{
    public void Execute()
    {
        Task.Run(async () =>
        {
            try
            {
                await PythonInitializer.InitializeAsync().ConfigureAwait(true);
                
                string scriptContent;
                try
                {
#if NETCOREAPP
                    scriptContent = await File.ReadAllTextAsync(scriptPath).ConfigureAwait(true);
#else
                    scriptContent = File.ReadAllText(scriptPath);
#endif
                }
                catch (IOException ex)
                {
                    Trace.TraceError($"Failed to read script file: {ex.Message}");
                    return;
                }
                
                var success = await ResolveDependenciesAsync(scriptContent).ConfigureAwait(true);
                if (!success)
                {
                    Trace.TraceWarning("Execution cancelled: Dependency resolution failed or was cancelled by user.");
                    return;
                }
        
                ExternalEventController.ActionEventHandler.Raise(_ =>
                {
                    PythonExecutor.ExecuteScript(scriptPath, scriptContent, rootPath);
                });
            }
            catch (Exception ex)
            {
                Trace.TraceError($"Python execution pipeline failed: {ex.Message}{Environment.NewLine}{ex.StackTrace}");
            }
        });
    }

    public static async Task<bool> ResolveDependenciesAsync(string scriptContent)
    {
        // 1. Parse PEP 723 metadata
        List<string> dependencies;
        try
        {
            dependencies = Pep723Parser.ParseDependencies(scriptContent);
        }
        catch (Exception ex)
        {
            Trace.TraceError($"Failed to parse PEP 723 metadata: {ex.Message}");
            return false;
        }

        // 2. No dependencies or empty list -> success
        if (dependencies.Count == 0) 
            return true;

        // 3. Check if installation is needed
        bool needsInstall;
        try 
        {
            needsInstall = await PythonDependencyManager.NeedsInstallationAsync(dependencies).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Failed to check dependencies status, will attempt install. Error: {ex.Message}");
            needsInstall = true;
        }
        
        if (!needsInstall)
        {
            return true;
        }

        Trace.TraceInformation("Started installation/upgrade dependencies.");
        return await ShowInstallDialogAsync(dependencies).ConfigureAwait(true);
    }

    private static Task<bool> ShowInstallDialogAsync(List<string> packages)
    {
        var tcs = new TaskCompletionSource<bool>();

        DispatcherHelper.RunOnMainThread(() =>
        {
            var vm = new PackageInstallViewModel();
            var window = new PackageInstallWindow(vm)
            {
                Owner = UIFramework.MainWindow.getMainWnd()
            };

            window.Loaded += async (_, _) =>
            {
                try
                {
                    var progress = new Progress<string>(msg => vm.UpdateProgress(msg));

                    await PythonDependencyManager.InstallDependenciesAsync(
                        packages, 
                        progress, 
                        CancellationToken.None).ConfigureAwait(true);
                    
                    vm.OnInstallationComplete(true);
                }
                catch (Exception ex)
                {
                    vm.UpdateProgress($"Error: {ex.Message}");
                    vm.OnInstallationComplete(false);
                }
            };
            
            window.Closed += (_, _) =>
            {
                tcs.TrySetResult(window.DialogResult == true);
            };

            window.ShowDialog();
        });

        return tcs.Task;
    }
}