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

/// <summary>
/// Embedded IronPython 3.4 host
/// </summary>
internal static class IronPythonRunner
{
    internal static ExecutionResult Execute(string scriptPath, string rootPath, IIronPythonBridge bridge)
    {
        ScriptEngine? engine = null;
        try
        {
            engine = CreateEngine(scriptPath, rootPath, bridge);
            return CompileAndExecute(engine, scriptPath);
        }
        catch (Exception ex)
        {
            return ExecutionResult.Failed(FormatExceptionChain(ex), ex);
        }
        finally
        {
            ShutdownEngine(engine);
        }
    }

    private static ScriptEngine CreateEngine(string scriptPath, string rootPath, IIronPythonBridge bridge)
    {
        var engine = Ipy.CreateEngine();
        bridge.ConfigureEngine(engine);
        IronPythonInitializer.AddStdLib(engine);
        IronPythonInitializer.Setup(engine);

        var paths = engine.GetSearchPaths();
        foreach (var dir in IronPythonSearchPaths.ForNativeHost(scriptPath, rootPath))
            paths.Add(dir);

        engine.SetSearchPaths(paths);
        return engine;
    }

    private static ExecutionResult CompileAndExecute(ScriptEngine engine, string scriptPath)
    {
        var scope = engine.CreateScope();
        scope.SetVariable("__file__", scriptPath);

        var script = engine.CreateScriptSourceFromFile(scriptPath, Encoding.UTF8, SourceCodeKind.File);
        var compilerOptions = (PythonCompilerOptions)engine.GetCompilerOptions(scope);
        compilerOptions.ModuleName = "__main__";
        compilerOptions.Module |= ModuleOptions.Initialize;

        var errors = new CompileErrorListener();
        var command = script.Compile(compilerOptions, errors);
        if (command is null)
            return ExecutionResult.Failed(FormatCompileErrors(errors));

        return ExecuteCompiledCommand(engine, command, scope);
    }

    private static ExecutionResult ExecuteCompiledCommand(
        ScriptEngine engine,
        CompiledCode command,
        ScriptScope scope)
    {
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
            return ExecutionResult.Failed(FormatExecutionException(engine, exception), exception);
        }
    }

    private static string FormatCompileErrors(CompileErrorListener errors)
    {
        var text = string.Join(Environment.NewLine, errors.Errors);
        return string.IsNullOrEmpty(text)
            ? "IronPython compile failed."
            : $"IronPython compile errors:{Environment.NewLine}{text}";
    }

    private static string FormatExecutionException(ScriptEngine engine, Exception exception)
    {
        var dotnet = string.Join("\n", "Script host traceback:", exception.ToString().Replace("\r\n", "\n"));
        var ipy = engine.GetService<ExceptionOperations>().FormatException(exception);
        ipy = string.Join("\n", "IronPython traceback:", ipy.Replace("\r\n", "\n"));
        return ipy + "\n\n" + dotnet;
    }

    private static void ShutdownEngine(ScriptEngine? engine)
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

    private static string FormatExceptionChain(Exception ex)
    {
        var parts = new List<string>();
        for (var e = ex; e != null && parts.Count < 10; e = e.InnerException)
            parts.Add($"{e.GetType().Name}: {e.Message}");

        return string.Join($"{Environment.NewLine} -> ", parts);
    }
}
