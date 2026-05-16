using System.Diagnostics;
using DevTools.Execution.Providers.Python;
using IronPython.Hosting;
using IronPython.Modules;
using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;

namespace DevTools.Execution.Providers.IronPython;

/// <summary>
/// Embedded IronPython bootstrap: host setup script + stdlib zip on <see cref="ScriptEngine"/>.
/// </summary>
internal static class IronPythonInitializer
{
    private const string StdLibResourceSuffix = "IronPython.StdLib.3.4.2.zip";
    private static string? _stdlibResourceName;

    /// <summary>
    /// Registers embedded setup script and logging function on the engine. Must be called before executing any script to ensure setup is applied.
    /// </summary>
    internal static void Setup(ScriptEngine engine)
    {
        var builtin = engine.GetBuiltinModule();
        // ReSharper disable once ConvertToLocalFunction
        Action<object> logFunction = obj =>
        {
            if (obj is string str)
                Trace.Write(str);
            else
                Trace.Write(obj);
        };

        builtin.SetVariable("__log_func__", logFunction);
        var script = engine.CreateScriptSourceFromString(
            PythonEmbedded.SetupScript,
            PythonEmbedded.SetupScriptFileName,
            SourceCodeKind.File);
        script.Execute(engine.CreateScope());
    }

    internal static void AddStdLib(ScriptEngine engine)
    {
        var asm = typeof(IronPythonInitializer).Assembly;
        _stdlibResourceName ??= asm.GetManifestResourceNames().SingleOrDefault(static n =>
            n.EndsWith(StdLibResourceSuffix, StringComparison.Ordinal));

        if (_stdlibResourceName is null)
            throw new InvalidOperationException($"Manifest resource ending with '{StdLibResourceSuffix}' was not found.");

        var importer = new ResourceMetaPathImporter(asm, _stdlibResourceName);
        dynamic sys = engine.GetSysModule();
        sys.meta_path.append(importer);
    }
}
