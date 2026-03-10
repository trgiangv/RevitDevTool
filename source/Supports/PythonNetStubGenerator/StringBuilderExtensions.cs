using System.Text;

namespace PythonNetStubGenerator;

public static class StringBuilderExtensions
{
    // Pre-computed indent strings to avoid per-call loop allocations
    private static readonly string[] IndentCache = Enumerable.Range(0, 16)
        .Select(i => new string(' ', i * 4))
        .ToArray();

    public static StringBuilder Indent(this StringBuilder sb)
    {
        var level = IndentScope.IndentLevel;
        sb.Append(level < IndentCache.Length ? IndentCache[level] : new string(' ', level * 4));
        return sb;
    }

    public static string CommaJoin(this IEnumerable<string?> strings) => string.Join(", ", strings);
}

public sealed class IndentScope : IDisposable
{
    public static int IndentLevel;
    public IndentScope() => IndentLevel++;
    public void Dispose() => IndentLevel--;
}