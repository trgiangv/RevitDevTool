using System.IO;
using System.Text;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using IronPython.Compiler;
using IronPython.Hosting;
using IronPython.Runtime;
using IronPython.Runtime.Exceptions;
using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;

namespace DevTools.Execution.Providers.IronPython;

/// <summary>
/// Embedded IronPython 3.4 host
/// </summary>
internal static class IronPythonExecutor
{
    internal static ExecutionResult Execute(
        string scriptPath,
        string rootPath,
        IIronPythonBridge bridge,
        IronPythonInitializer initializer)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(initializer);
            var engine = initializer.GetOrCreateEngine(bridge);
            RefreshScriptSearchPaths(engine, scriptPath, rootPath);

            var isDriver = IsIpyTestDriverScript(scriptPath);
            if (isDriver)
                SetPytestRunning(engine, true);

            try
            {
                IronPythonDebugger.EnsureCurrentThreadTraced(engine);
                return CompileAndExecute(engine, scriptPath);
            }
            finally
            {
                if (isDriver)
                    SetPytestRunning(engine, false);
            }
        }
        catch (Exception ex)
        {
            return ExecutionResult.Failed(FormatExceptionChain(ex), ex);
        }
    }

    internal static bool IsIpyTestDriverScript(string scriptPath) =>
        string.Equals(Path.GetFileName(scriptPath), "IpyTestDriver.py", StringComparison.OrdinalIgnoreCase);

    private static void RefreshScriptSearchPaths(ScriptEngine engine, string scriptPath, string rootPath)
    {
        var paths = engine.GetSearchPaths().ToList();
        foreach (var dir in IronPythonSearchPaths.ForNativeHost(scriptPath, rootPath))
            AppendUnique(paths, dir);

        engine.SetSearchPaths(paths);
    }

    private static void AppendUnique(ICollection<string> paths, string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
            return;

        if (paths.Any(p => string.Equals(p, directory, StringComparison.OrdinalIgnoreCase)))
            return;

        paths.Add(directory);
    }

    private static void SetPytestRunning(ScriptEngine engine, bool value)
    {
        if (value)
        {
            dynamic sys = engine.GetSysModule();
            sys.__pytest_running__ = true;
            return;
        }

        engine.CreateScriptSourceFromString(
                "import sys\n" +
                "if hasattr(sys, '__pytest_running__'):\n" +
                "    del sys.__pytest_running__\n")
            .Execute(engine.CreateScope());
    }

    private static ExecutionResult CompileAndExecute(ScriptEngine engine, string scriptPath)
    {
        var canonicalPath = Path.GetFullPath(scriptPath);
        var sourceText = File.ReadAllText(canonicalPath, Encoding.UTF8);

        var scope = engine.CreateScope();
        scope.SetVariable("__file__", canonicalPath);
        scope.SetVariable("__name__", "__main__");

        // Same contract as CPython: compile(source, __file__, 'exec') so
        // co_filename is the on-disk path VS Code binds breakpoints to — not
        // a DLR file URI / <string>.
        var script = engine.CreateScriptSourceFromString(
            sourceText,
            canonicalPath,
            SourceCodeKind.File);
        var compilerOptions = (PythonCompilerOptions)engine.GetCompilerOptions(scope);
        compilerOptions.ModuleName = "__main__";
        compilerOptions.Module |= ModuleOptions.Initialize;

        var errors = new CompileErrorListener();
        var command = script.Compile(compilerOptions, errors);
        return command is null
            ? ExecutionResult.Failed(FormatCompileErrors(errors))
            : ExecuteCompiledCommand(engine, command, scope);
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
        catch (SystemExitException ex)
        {
            var code = ex.GetExitCode(out _);
            return code == 0
                ? ExecutionResult.Succeeded("IronPython script exited.")
                : ExecutionResult.Failed($"IronPython script exited with code {code}.");
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

    private static string FormatExceptionChain(Exception ex)
    {
        var parts = new List<string>();
        for (var e = ex; e != null && parts.Count < 10; e = e.InnerException)
            parts.Add($"{e.GetType().Name}: {e.Message}");

        return string.Join($"{Environment.NewLine} -> ", parts);
    }
}
