using System.IO;

namespace DevTools.Execution.Providers;

/// <summary>Entry script rule: which files are entry scripts and which <see cref="ExecutionMode"/> runs them.</summary>
public static class ScriptEntryMode
{
    public const string IronPythonEntryFileSuffix = "_ipy_script.py";

    private static readonly string[] EntryFileSuffixes = ["script.py", "script.fsx", "script.csx"];

    public static IReadOnlyList<string> EntryPatterns { get; } = EntryFileSuffixes.Select(suffix => $"*{suffix}").ToArray();

    public static bool IsEntryScript(string scriptPath)
    {
        var fileName = Path.GetFileName(scriptPath);
        return EntryFileSuffixes.Any(suffix => fileName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }

    public static bool TryResolve(string scriptPath, out ExecutionMode mode)
    {
        mode = Path.GetExtension(scriptPath).ToLowerInvariant() switch
        {
            ".py" when IsIronPythonEntryScript(scriptPath) => ExecutionMode.IronPython,
            ".py" => ExecutionMode.Python,
            ".fsx" => ExecutionMode.FSharp,
            ".csx" => ExecutionMode.CSharp,
            _ => ExecutionMode.Unsupported
        };
        return mode != ExecutionMode.Unsupported;
    }

    public static bool IsIronPythonEntryScript(string scriptPath) =>
        scriptPath.EndsWith(IronPythonEntryFileSuffix, StringComparison.OrdinalIgnoreCase);
}
