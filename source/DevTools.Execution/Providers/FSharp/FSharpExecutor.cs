using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using FSharp.Compiler.Diagnostics;
using FSharp.Compiler.Interactive;
using Microsoft.FSharp.Core;
namespace DevTools.Execution.Providers.FSharp;

/// <summary>
/// Creates an FsiEvaluationSession and evaluates an F# script.
/// Uses <see cref="IFSharpHostSupport"/> for host-specific type discovery and session references.
/// </summary>
internal static class FSharpExecutor
{
    public static FSharpCompilationOutput CreateSessionAndEvaluate(
        string resolvedScriptPath,
        string[] references,
        IFSharpHostSupport hostSupport)
    {
        var assemblySnapshot = new HashSet<Assembly>(AppDomain.CurrentDomain.GetAssemblies());

        var sbOut = new StringBuilder();
        var sbErr = new StringBuilder();
        using var outWriter = new StringWriter(sbOut);
        using var errWriter = new StringWriter(sbErr);

        try
        {
            var sessionReferences = hostSupport.GetSessionReferences();
            var allRefs = new HashSet<string>(sessionReferences, StringComparer.OrdinalIgnoreCase);
            allRefs.UnionWith(references.Where(r => !string.IsNullOrWhiteSpace(r)));

            var argv = BuildSessionArgs(allRefs);
            var fsiConfig = Shell.FsiEvaluationSession.GetDefaultConfiguration();

            var prevDir = Environment.CurrentDirectory;
            var fsharpCoreDir = Path.GetDirectoryName(typeof(FSharpOption<>).Assembly.Location);
            if (!string.IsNullOrEmpty(fsharpCoreDir))
                Environment.CurrentDirectory = fsharpCoreDir;

            Shell.FsiEvaluationSession session;
            try
            {
                session = Shell.FsiEvaluationSession.Create(
                    fsiConfig: fsiConfig,
                    argv: argv,
                    inReader: new StringReader(""),
                    outWriter: outWriter,
                    errorWriter: errWriter,
                    collectible: FSharpOption<bool>.Some(true),
                    legacyReferenceResolver: null);
            }
            finally
            {
                Environment.CurrentDirectory = prevDir;
            }

            var (choice, diagnostics) = session.EvalScriptNonThrowing(resolvedScriptPath);
            FlushOutput(sbOut, sbErr);
            ReportDiagnostics(diagnostics);

            if (choice.IsChoice2Of2)
            {
                var exn = ((FSharpChoice<Unit, Exception>.Choice2Of2)choice).Item;
                Trace.TraceError($"F# script evaluation failed: {exn.Message}{Environment.NewLine}{exn.StackTrace}");
                DisposeSession(session);
                return new FSharpCompilationOutput(null, null);
            }

            var commandInstance = hostSupport.FindAndCreateCommand(assemblySnapshot);
            if (commandInstance != null)
                return new FSharpCompilationOutput(commandInstance, session);

            Trace.TraceError("No executable command type found in F# script.");
            DisposeSession(session);
            return new FSharpCompilationOutput(null, null);
        }
        catch (Exception ex)
        {
            FlushOutput(sbOut, sbErr);
            Trace.TraceError($"F# compilation error: {ex}");

            var inner = ex.InnerException;
            while (inner != null)
            {
                Trace.TraceError($"F# inner exception: {inner}");
                inner = inner.InnerException;
            }

            Trace.TraceError(
                $"F# runtime context -> CWD: '{Environment.CurrentDirectory}', " +
                $"FSharp.Core: '{typeof(FSharpOption<>).Assembly.Location}', " +
                $"FCS: '{typeof(Shell.FsiEvaluationSession).Assembly.Location}'");
            return new FSharpCompilationOutput(null, null);
        }
    }
    
    private static object? FindAndCreateCommand(this IFSharpHostSupport fSharpHost, HashSet<Assembly> assemblySnapshot)
    {
        var current = new HashSet<Assembly>(AppDomain.CurrentDomain.GetAssemblies());
        current.ExceptWith(assemblySnapshot);
        return current
            .Select(fSharpHost.TryFindCommandType)
            .OfType<Type>()
            .Select(Activator.CreateInstance)
            .FirstOrDefault();
    }

    public static ExecutionResult ExecuteCommand(object compiledCommand, ICommandRunner commandRunner)
    {
        return commandRunner.RunFSharpCommand(compiledCommand);
    }

    private static void DisposeSession(Shell.FsiEvaluationSession session) =>
        ((IDisposable)session).Dispose();

    private static string[] BuildSessionArgs(IEnumerable<string> allReferences)
    {
        var args = new List<string>
        {
            "fsi.exe",
            "--noninteractive",
            "--nologo",
#if NETFRAMEWORK
            "--multiemit-",
#else
            "--multiemit+",
#endif
        };

        args.AddRange(allReferences.Select(reference => $"--reference:{reference}"));
        return args.ToArray();
    }

    private static void FlushOutput(StringBuilder sbOut, StringBuilder sbErr)
    {
        if (sbOut.Length > 0)
        {
            Trace.Write(sbOut.ToString());
            sbOut.Clear();
        }

        if (sbErr.Length <= 0) return;
        Trace.TraceError(sbErr.ToString());
        sbErr.Clear();
    }

    private static void ReportDiagnostics(FSharpDiagnostic[] diagnostics)
    {
        foreach (var diag in diagnostics)
        {
            var msg = ToDiagnosticMessage(diag);
            if (diag.Severity.IsError)
                Trace.TraceError(msg);
            else if (diag.Severity.IsWarning)
                Trace.TraceWarning(msg);
            else
                Debug.WriteLine(msg);
        }
    }

    private static string ToDiagnosticMessage(FSharpDiagnostic diag)
    {
        return $"{diag.FileName}({diag.StartLine},{diag.StartColumn}): {diag.Message}";
    }
}

internal readonly record struct FSharpCompilationOutput(
    object? Command,
    Shell.FsiEvaluationSession? Session);
