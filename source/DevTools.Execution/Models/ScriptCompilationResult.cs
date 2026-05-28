namespace DevTools.Execution.Models;

/// <summary>
/// Unified result of script compilation (C# or F#).
/// Carries the compiled command instance on success, or diagnostics on failure.
/// Optionally holds a cleanup disposable (e.g., ALC or FSI session) for the cache to own.
/// </summary>
public sealed class ScriptCompilationResult
{
    public bool Success { get; private init; }
    public object? Command { get; private init; }
    public IDisposable? Cleanup { get; private init; }
    private IReadOnlyList<string> Diagnostics { get; init; } = [];

    public static ScriptCompilationResult Succeeded(object command, IDisposable? cleanup = null) =>
        new() { Success = true, Command = command, Cleanup = cleanup };

    public static ScriptCompilationResult Failed(params string[] diagnostics) =>
        new() { Success = false, Diagnostics = diagnostics };

    public static ScriptCompilationResult Failed(IReadOnlyList<string> diagnostics) =>
        new() { Success = false, Diagnostics = diagnostics };

    public string FormatDiagnostics(string fallbackMessage = "Compilation failed.") =>
        Diagnostics.Count > 0
            ? string.Join(Environment.NewLine, Diagnostics)
            : fallbackMessage;
}
