using System.Diagnostics;
using System.Text;

namespace RevitDevTool.Utils;

public static class StackTraceUtils
{
    /// <summary>
    /// Default namespace prefixes to skip when building stack traces.
    /// </summary>
    private static readonly string[] DefaultIgnoredNamespacePrefixes =
    [
        "Serilog",
        "Nlog",
        "Microsoft.Extensions.Logging",
        "MS.Internal",
        "System.Environment.get_StackTrace",
        "System.Diagnostics",
        "RevitDevTool",
        "Autodesk.Revit.UI",
        "revitAPIStartupFromSingleManifest"
    ];

    /// <summary>
    /// Default class name patterns to skip when building stack traces.
    /// </summary>
    private static readonly string[] DefaultIgnoredClassPatterns =
    [
        "TraceListener",
        "SerilogTraceListener",
        "MethodBaseInvoker",
        "RuntimeMethodHandle",
        "Debugger",
        "DebugSink",
        "RestrictedSink",
        "AsyncSink",
        "SafeAggregateSink",
        "StackTraceUtils"
    ];

    /// <summary>
    /// Builds a formatted stack trace string from TraceEventCache callstack.
    /// </summary>
    /// <param name="eventCache">Trace event cache containing callstack.</param>
    /// <param name="maxDepth">Maximum number of stack frames to include.</param>
    /// <param name="ignoredNamespacePrefixes">
    ///   Custom namespace prefixes to ignore on raw lines (null uses defaults).
    /// </param>
    /// <param name="ignoredClassPatterns">
    ///   Custom class patterns to ignore on simplified Class.Method (null uses defaults).
    /// </param>
    public static string BuildStackTrace(
        TraceEventCache? eventCache,
        int maxDepth,
        IReadOnlyList<string>? ignoredNamespacePrefixes = null,
        IReadOnlyList<string>? ignoredClassPatterns = null)
    {
        if (maxDepth <= 0)
            return string.Empty;

        var callstack = eventCache?.Callstack;
        if (callstack == null || string.IsNullOrWhiteSpace(callstack))
            return string.Empty;

        return ParseCallstackString(
            callstack,
            maxDepth,
            ignoredNamespacePrefixes ?? DefaultIgnoredNamespacePrefixes,
            ignoredClassPatterns ?? DefaultIgnoredClassPatterns);
    }

    /// <summary>
    /// Parses a callstack string and formats it with filtering.
    /// </summary>
    /// <param name="callstack">Raw callstack string from TraceEventCache.</param>
    /// <param name="maxDepth">Maximum number of stack frames to include.</param>
    /// <param name="ignoredNamespacePrefixes">
    ///   Namespace prefixes to ignore (checked against raw line after "at ").
    /// </param>
    /// <param name="ignoredClassPatterns">
    ///   Class patterns to ignore (checked against simplified Class.Method).
    /// </param>
    private static string ParseCallstackString(
        string callstack,
        int maxDepth,
        IReadOnlyList<string> ignoredNamespacePrefixes,
        IReadOnlyList<string> ignoredClassPatterns)
    {
        if (maxDepth <= 0 || string.IsNullOrWhiteSpace(callstack))
            return string.Empty;

        var lines = callstack.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var stack = new StringBuilder();
        var taken = 0;

        foreach (var line in lines)
        {
            if (taken >= maxDepth) break;
            if (!ProcessCallstackLine(line, ignoredNamespacePrefixes, ignoredClassPatterns, out var methodInfo)) continue;
            AppendMethodInfo(stack, methodInfo);
            taken++;
        }

        return stack.ToString();
    }

    /// <summary>
    /// Attempts to process a single callstack line and extract method information.
    /// </summary>
    /// <param name="line">The callstack line to process.</param>
    /// <param name="ignoredNamespacePrefixes">Namespace prefixes to ignore.</param>
    /// <param name="ignoredClassPatterns">Class patterns to ignore.</param>
    /// <param name="methodInfo">The extracted method information if successful.</param>
    /// <returns>True if the line was successfully processed and should be included.</returns>
    private static bool ProcessCallstackLine(
        string line,
        IReadOnlyList<string> ignoredNamespacePrefixes,
        IReadOnlyList<string> ignoredClassPatterns,
        out string methodInfo)
    {
        methodInfo = string.Empty;

        var trimmedLine = line.Trim();
        if (trimmedLine.Length == 0)
            return false;

        if (ShouldSkipByNamespace(trimmedLine, ignoredNamespacePrefixes))
            return false;

        methodInfo = ExtractMethodFromCallstackLine(trimmedLine);
        if (string.IsNullOrEmpty(methodInfo))
            return false;

        return !ShouldSkipByClassPattern(methodInfo, ignoredClassPatterns);
    }

    /// <summary>
    /// Appends method information to the stack trace string builder.
    /// </summary>
    private static void AppendMethodInfo(StringBuilder stack, string methodInfo)
    {
        if (stack.Length > 0)
            stack.Append(" > ");

        stack.Append(methodInfo);
    }

    /// <summary>
    /// Extracts simplified method information from a callstack line.
    /// Example raw:
    ///   "   at MyNamespace.MyClass.MyMethod() in C:\path\file.cs:line 42"
    /// Returns:
    ///   "MyNamespace.MyClass.MyMethod:42" or "MyNamespace.MyClass.MyMethod"
    /// </summary>
    private static string ExtractMethodFromCallstackLine(string line)
    {
        // Remove "at " prefix if present
        var atIndex = line.IndexOf("at ", StringComparison.Ordinal);
        if (atIndex >= 0)
            line = line.Substring(atIndex + 3).Trim();

        if (line.Length == 0)
            return string.Empty;

        // Extract the method part (before " in ")
        var inIndex = line.IndexOf(" in ", StringComparison.Ordinal);
        var methodPart = inIndex >= 0
            ? line.Substring(0, inIndex).Trim()
            : line.Trim();

        // Extract line number if present (":line N")
        var lineNumber = string.Empty;
        if (inIndex >= 0)
        {
            const string linePrefix = ":line ";
            var lineIndex = line.IndexOf(linePrefix, inIndex, StringComparison.Ordinal);
            if (lineIndex >= 0)
            {
                var lineNumStart = lineIndex + linePrefix.Length;
                var lineNumEnd = line.IndexOfAny([' ', '\t', '\r', '\n'], lineNumStart);
                lineNumber = lineNumEnd >= 0
                    ? line.Substring(lineNumStart, lineNumEnd - lineNumStart)
                    : line.Substring(lineNumStart);
            }
        }

        // Simplify from Namespace.SubNamespace.Class.Method -> Class.Method
        // var lastDot = methodPart.LastIndexOf('.');
        // if (lastDot > 0)
        // {
        //     var beforeLastDot = methodPart.LastIndexOf('.', lastDot - 1);
        //     if (beforeLastDot >= 0 && beforeLastDot + 1 < methodPart.Length)
        //     {
        //         methodPart = methodPart.Substring(beforeLastDot + 1);
        //     }
        // }

        // Remove parameter list
        var parenIndex = methodPart.IndexOf('(');
        if (parenIndex >= 0)
            methodPart = methodPart.Substring(0, parenIndex);

        if (methodPart.Length == 0)
            return string.Empty;

        // Append line number if available
        if (!string.IsNullOrEmpty(lineNumber))
            methodPart += ":" + lineNumber;

        return methodPart;
    }

    /// <summary>
    /// Determines if a raw callstack line should be skipped based on namespace prefixes.
    /// This checks the full symbol "Namespace.Type.Method(...)".
    /// </summary>
    private static bool ShouldSkipByNamespace(string rawLine, IReadOnlyList<string> ignoredNamespacePrefixes)
    {
        // Normalize: remove leading "at "
        var text = rawLine;
        var atIndex = text.IndexOf("at ", StringComparison.Ordinal);
        if (atIndex >= 0)
            text = text.Substring(atIndex + 3).Trim();

        if (text.Length == 0)
            return false;

        // We only care about the "Namespace.Type.Method(...)" part
        // Cut off " in ..." if present
        var inIndex = text.IndexOf(" in ", StringComparison.Ordinal);
        if (inIndex >= 0)
            text = text.Substring(0, inIndex).Trim();

        // ReSharper disable once ForCanBeConvertedToForeach
        // ReSharper disable once LoopCanBeConvertedToQuery
        for (var i = 0; i < ignoredNamespacePrefixes.Count; i++)
        {
            if (text.StartsWith(ignoredNamespacePrefixes[i], StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Determines if a simplified method info (Class.Method[:line]) should be skipped
    /// based on class patterns.
    /// </summary>
    private static bool ShouldSkipByClassPattern(string methodInfo, IReadOnlyList<string> ignoredClassPatterns)
    {
        // ReSharper disable once ForCanBeConvertedToForeach
        // ReSharper disable once LoopCanBeConvertedToQuery
        for (var i = 0; i < ignoredClassPatterns.Count; i++)
        {
            if (methodInfo.Contains(ignoredClassPatterns[i], StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}