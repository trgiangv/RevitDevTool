using Microsoft.Scripting;
using Microsoft.Scripting.Hosting;

namespace DevTools.Execution.Providers.IronPython;

internal sealed class CompileErrorListener : ErrorListener
{
    private readonly List<string> _errors = [];

    internal IReadOnlyList<string> Errors => _errors;

    public override void ErrorReported(ScriptSource? source, string message, SourceSpan span, int errorCode, Severity severity)
    {
        var path = source?.Path ?? "unknown";
        _errors.Add($"{path} ({span.Start.Line},{span.Start.Column}): {message}");
    }
}
