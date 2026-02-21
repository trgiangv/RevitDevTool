using System.Diagnostics;
using System.IO;
using Python.Runtime;
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
                
                var success = await ResolveDependenciesAsync(scriptPath).ConfigureAwait(true);
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

    public static async Task<bool> ResolveDependenciesAsync(string scriptPath)
    {
        // 1. Parse PEP 723 metadata
        List<string> dependencies;
        try
        {
            dependencies = await PythonDepsManager.ResolveDependenciesAsync(scriptPath).ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Trace.TraceError($"Failed to parse PEP 723 metadata: {ex.Message}");
            return false;
        }

        // 2. No dependencies or empty list -> success
        if (dependencies.Count == 0)
            return true;

        Trace.TraceInformation($"Installing {dependencies.Count} dependency(s) via pixi...");
        var installed = await ShowInstallDialogAsync(dependencies).ConfigureAwait(true);
        
        if (installed)
        {
            RefreshImportCache();
        }
        
        return installed;
    }

    /// <summary>
    /// After uv installs new packages on disk, Python's PathFinder caches
    /// the directory listings from sys.path entries. invalidate_caches()
    /// forces it to rescan on the next import.
    /// </summary>
    private static void RefreshImportCache()
    {
        if (!PythonInitializer.IsInitialized) return;
        
        try
        {
            using (Py.GIL())
            {
                using var scope = Py.CreateScope();
                scope.Exec("""
                            import importlib
                            import site
                            import sys
                            
                            importlib.invalidate_caches()
                            
                            for sp in site.getsitepackages():
                                if sp not in sys.path:
                                    sys.path.insert(0, sp)
                            
                            user_site = site.getusersitepackages()
                            if isinstance(user_site, str) and user_site not in sys.path:
                                sys.path.insert(0, user_site)
                            """);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"Failed to refresh Python import cache: {ex.Message}");
        }
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

                    await PythonDepsManager.InstallDependenciesAsync(
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