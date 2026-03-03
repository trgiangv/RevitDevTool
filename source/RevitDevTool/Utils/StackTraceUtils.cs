using System.Diagnostics;
using System.IO;
using System.Text;
// ReSharper disable ReplaceSubstringWithRangeIndexer
// ReSharper disable ForCanBeConvertedToForeach
// ReSharper disable LoopCanBeConvertedToQuery
#pragma warning disable IDE0057

namespace RevitDevTool.Utils;

public static class StackTraceUtils
{
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
        "revitAPIStartupFromSingleManifest",
        "IronPython.Runtime.Operations",
        "IronPython.Runtime.Binding",
        "IronPython.Compiler",
        "Microsoft.Scripting.Runtime",
        "Microsoft.Scripting.Interpreter"
    ];

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
        "StackTraceUtils",
        "CallSite",
        "UpdateDelegates",
        "LightLambda"
    ];

    /// <summary>
    /// Builds a formatted stack trace string from TraceEventCache callstack.
    /// </summary>
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

    private static string ParseCallstackString(
        string callstack,
        int maxDepth,
        IReadOnlyList<string> ignoredNamespacePrefixes,
        IReadOnlyList<string> ignoredClassPatterns)
    {
        if (maxDepth <= 0 || string.IsNullOrWhiteSpace(callstack))
            return string.Empty;

        var stack = new StringBuilder();
        var taken = 0;
        using var reader = new StringReader(callstack);
        while (taken < maxDepth)
        {
            var line = reader.ReadLine();
            if (line == null)
            {
                break;
            }

            if (!ProcessCallstackLine(line, ignoredNamespacePrefixes, ignoredClassPatterns, out var methodInfo)) continue;
            AppendMethodInfo(stack, methodInfo);
            taken++;
        }

        return stack.ToString();
    }

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

        methodInfo = ExtractMethodFromCallstackLine(trimmedLine, ignoredNamespacePrefixes);
        if (string.IsNullOrEmpty(methodInfo))
            return false;

        return !ShouldSkipByClassPattern(methodInfo, ignoredClassPatterns);
    }

    private static void AppendMethodInfo(StringBuilder stack, string methodInfo)
    {
        if (stack.Length > 0)
            stack.Append(" > ");

        stack.Append(methodInfo);
    }

    private static string ExtractMethodFromCallstackLine(string line, IReadOnlyList<string> ignoredNamespacePrefixes)
    {
        var atIndex = line.IndexOf("at ", StringComparison.Ordinal);
        if (atIndex >= 0)
            line = line.Substring(atIndex + 3).Trim();

        if (line.Length == 0)
            return string.Empty;

        var inIndex = line.IndexOf(" in ", StringComparison.Ordinal);
        var methodPart = inIndex >= 0
            ? line.Substring(0, inIndex).Trim()
            : line.Trim();

        if (ShouldSkipByNamespace(methodPart, ignoredNamespacePrefixes))
        {
            return string.Empty;
        }

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

        var parenIndex = methodPart.IndexOf('(');
        if (parenIndex >= 0)
            methodPart = methodPart.Substring(0, parenIndex);

        if (methodPart.Length == 0)
            return string.Empty;

        if (!string.IsNullOrEmpty(lineNumber))
            methodPart += ":" + lineNumber;

        return methodPart;
    }

    private static bool ShouldSkipByNamespace(string methodPart, IReadOnlyList<string> ignoredNamespacePrefixes)
    {
        if (methodPart.Length == 0)
            return false;

        for (var i = 0; i < ignoredNamespacePrefixes.Count; i++)
        {
            if (methodPart.StartsWith(ignoredNamespacePrefixes[i], StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static bool ShouldSkipByClassPattern(string methodInfo, IReadOnlyList<string> ignoredClassPatterns)
    {
        for (var i = 0; i < ignoredClassPatterns.Count; i++)
        {
            if (methodInfo.IndexOf(ignoredClassPatterns[i], StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }
}
