using System.IO;
using System.Text;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using IronPython.Compiler;
using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;
using Ipy = IronPython.Hosting.Python;

namespace DevTools.Execution.Providers.IronPython;

internal static class IronPythonScriptRunner
{
    internal static ExecutionResult Execute(
        string scriptPath,
        string rootPath,
        IIronPythonBridge bridge)
    {
        ScriptEngine? engine = null;
        try
        {
            engine = Ipy.CreateEngine();
            bridge.ConfigureEngine(engine);
            IronPythonInitializer.AddStdLib(engine);
            IronPythonInitializer.Setup(engine);

            var paths = engine.GetSearchPaths();
            var scriptDir = Path.GetDirectoryName(scriptPath);
            if (!string.IsNullOrEmpty(scriptDir))
                paths.Add(scriptDir);
            if (!string.IsNullOrWhiteSpace(rootPath))
                paths.Add(rootPath);
            engine.SetSearchPaths(paths);

            var scope = engine.CreateScope();
            scope.SetVariable("__file__", scriptPath);

            var script = engine.CreateScriptSourceFromFile(scriptPath, Encoding.UTF8, SourceCodeKind.File);
            var compilerOptions = (PythonCompilerOptions)engine.GetCompilerOptions(scope);
            compilerOptions.ModuleName = "__main__";
            compilerOptions.Module |= ModuleOptions.Initialize;

            var errors = new CompileErrorListener();
            var command = script.Compile(compilerOptions, errors);
            if (command == null)
            {
                var text = string.Join(Environment.NewLine, errors.Errors);
                return ExecutionResult.Failed(
                    string.IsNullOrEmpty(text) ? "IronPython compile failed." : $"IronPython compile errors:{Environment.NewLine}{text}");
            }

            try
            {
                command.Execute(scope);
                return ExecutionResult.Succeeded("IronPython script completed successfully.");
            }
            catch (SystemExitException)
            {
                return ExecutionResult.Succeeded("IronPython script exited.");
            }
            catch (Exception exception)
            {
                var dotnet = string.Join("\n", "Script host traceback:", exception.ToString().Replace("\r\n", "\n"));
                var ipy = engine.GetService<ExceptionOperations>().FormatException(exception);
                ipy = string.Join("\n", "IronPython traceback:", ipy.Replace("\r\n", "\n"));
                return ExecutionResult.Failed(ipy + "\n\n" + dotnet, exception);
            }
        }
        catch (Exception ex)
        {
            return ExecutionResult.Failed(FormatExceptionChain(ex), ex);
        }
        finally
        {
            try
            {
                engine?.Runtime.Shutdown();
            }
            catch
            {
                // ignored
            }
        }
    }

    /// <summary>
    /// Surfaces <see cref="TypeInitializationException"/> inner causes (ILRepack merge issues often hide here).
    /// </summary>
    private static string FormatExceptionChain(Exception ex)
    {
        var parts = new List<string>();
        for (var e = ex; e != null && parts.Count < 10; e = e.InnerException)
            parts.Add($"{e.GetType().Name}: {e.Message}");

        return string.Join($"{Environment.NewLine} -> ", parts);
    }
}
