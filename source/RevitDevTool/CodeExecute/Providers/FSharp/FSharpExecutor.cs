using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using FSharp.Compiler.Diagnostics;
using FSharp.Compiler.Interactive;
using Microsoft.FSharp.Core;

namespace RevitDevTool.CodeExecute.Providers.FSharp;

/// <summary>
/// Creates an FsiEvaluationSession and evaluates an F# script.
/// </summary>
internal static class FSharpExecutor
{
    public static FSharpCompilationOutput CreateSessionAndEvaluate(string resolvedScriptPath, string[] references)
    {
        var assemblySnapshot = new HashSet<Assembly>(AppDomain.CurrentDomain.GetAssemblies());

        var sbOut = new StringBuilder();
        var sbErr = new StringBuilder();
        using var outWriter = new StringWriter(sbOut);
        using var errWriter = new StringWriter(sbErr);

        try
        {
            var argv = BuildSessionArgs(references);
            var fsiConfig = Shell.FsiEvaluationSession.GetDefaultConfiguration();

            var prevDir = Environment.CurrentDirectory;
            var fsharpCoreDir = Path.GetDirectoryName(typeof(FSharpOption<>).Assembly.Location);
            if (!string.IsNullOrEmpty(fsharpCoreDir))
                Environment.CurrentDirectory = fsharpCoreDir; // https://github.com/dotnet/fsharp/issues/9064

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

            var commandType = FindCommandType(assemblySnapshot);
            if (commandType != null)
            {
                var command = (IExternalCommand)Activator.CreateInstance(commandType)!;
                return new FSharpCompilationOutput(command, session);
            }

            Trace.TraceError("No IExternalCommand with [Transaction] attribute found in F# script.");
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

    public static Result ExecuteCommand(
        IExternalCommand command,
        ExternalCommandData commandData,
        ref string message,
        ElementSet elements)
    {
        return command.Execute(commandData, ref message, elements);
    }

    private static void DisposeSession(Shell.FsiEvaluationSession session) =>
        ((IDisposable)session).Dispose();

    private static string[] BuildSessionArgs(IEnumerable<string> additionalReferences)
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

        var references = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            typeof(Document).Assembly.Location,
            typeof(UIApplication).Assembly.Location
        };

        foreach (var reference in additionalReferences)
        {
            if (!string.IsNullOrWhiteSpace(reference))
                references.Add(reference);
        }

        args.AddRange(references.Select(reference => $"--reference:{reference}"));

        return args.ToArray();
    }

    private static Type? FindCommandType(HashSet<Assembly> assemblySnapshot)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !assemblySnapshot.Contains(a))
            .SelectMany(SafeGetTypes)
            .FirstOrDefault(t =>
                typeof(IExternalCommand).IsAssignableFrom(t)
                && !t.IsAbstract
                && t.GetCustomAttribute<TransactionAttribute>() != null);
    }

    private static Type[] SafeGetTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            // ReSharper disable once RedundantSuppressNullableWarningExpression
            return ex.Types.Where(t => t != null).ToArray()!;
        }
        catch
        {
            return [];
        }
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
    IExternalCommand? Command,
    Shell.FsiEvaluationSession? Session);
