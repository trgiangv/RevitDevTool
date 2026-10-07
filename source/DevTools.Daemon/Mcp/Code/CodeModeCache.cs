using System.Collections.Concurrent;

namespace DevTools.Daemon.Mcp.Code;

/// <summary>Reuses emitted bytes for identical source. Does not retain a loaded assembly.</summary>
public sealed class CodeModeCache
{
    private readonly ConcurrentDictionary<string, byte[]> _images = new(StringComparer.Ordinal);

    public CompiledScript Load(string code)
    {
        var image = _images.GetOrAdd(code, static source => CodeModeCompiler.Emit(source));
        return CodeModeCompiler.Load(image);
    }

    public bool SharesImage(string left, string right) =>
        ReferenceEquals(
            _images.GetOrAdd(left, static source => CodeModeCompiler.Emit(source)),
            _images.GetOrAdd(right, static source => CodeModeCompiler.Emit(source)));
}
