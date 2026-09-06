using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using DevTools.Hosting;
using FSharp.Compiler.Diagnostics;
using FSharp.Compiler.Interactive;
using Microsoft.Extensions.Logging;
using Microsoft.FSharp.Core;
using ZLogger;

namespace DevTools.Execution.Providers.FSharp;

/// <summary>
/// Creates an FsiEvaluationSession and evaluates an F# script.
/// Uses <see cref="ICompiledScriptBridge"/> for host-specific type discovery and session references.
/// </summary>
public sealed class FSharpExecutor(ILogger<FSharpExecutor> logger, IHostAppInfo? hostApp = null)
{
    internal FSharpCompilationOutput CreateSessionAndEvaluate(
        string resolvedScriptPath,
        string[] references,
        ICompiledScriptBridge bridgeSupport)
    {
        var assemblySnapshot = new HashSet<Assembly>(AppDomain.CurrentDomain.GetAssemblies());

        var sbOut = new StringBuilder();
        var sbErr = new StringBuilder();
        using var outWriter = new StringWriter(sbOut);
        using var errWriter = new StringWriter(sbErr);

        try
        {
            var sessionReferences = bridgeSupport.GetSessionReferences();
            var allRefs = new HashSet<string>(sessionReferences, StringComparer.OrdinalIgnoreCase);
            allRefs.UnionWith(references.Where(r => !string.IsNullOrWhiteSpace(r)));

            var argv = BuildSessionArgs(allRefs, hostApp);
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
                logger.ZLogError($"F# script evaluation failed: {exn.Message}{Environment.NewLine}{exn.StackTrace}");
                DisposeSession(session);
                return new FSharpCompilationOutput(null, null);
            }

            var commandInstance = FindAndCreateCommand(bridgeSupport, assemblySnapshot);
            if (commandInstance != null)
                return new FSharpCompilationOutput(commandInstance, session);

            logger.ZLogError($"No executable command type found in F# script.");
            DisposeSession(session);
            return new FSharpCompilationOutput(null, null);
        }
        catch (Exception ex)
        {
            FlushOutput(sbOut, sbErr);
            logger.ZLogError($"F# compilation error: {ex}");

            var inner = ex.InnerException;
            while (inner != null)
            {
                logger.ZLogError($"F# inner exception: {inner}");
                inner = inner.InnerException;
            }

            logger.ZLogError(
                $"F# runtime context -> CWD: '{Environment.CurrentDirectory}', FSharp.Core: '{typeof(FSharpOption<>).Assembly.Location}', FCS: '{typeof(Shell.FsiEvaluationSession).Assembly.Location}'");
            return new FSharpCompilationOutput(null, null);
        }
    }

    private static object? FindAndCreateCommand(ICompiledScriptBridge fSharpBridge, HashSet<Assembly> assemblySnapshot)
    {
        var current = new HashSet<Assembly>(AppDomain.CurrentDomain.GetAssemblies());
        current.ExceptWith(assemblySnapshot);
        return current
            .Select(fSharpBridge.TryFindCommandType)
            .OfType<Type>()
            .Select(Activator.CreateInstance)
            .FirstOrDefault();
    }

    private static void DisposeSession(Shell.FsiEvaluationSession session) =>
        ((IDisposable)session).Dispose();

    private static string[] BuildSessionArgs(IEnumerable<string> allReferences, IHostAppInfo? hostApp)
    {
        var args = new List<string>
        {
            "fsi.exe",
            "--noninteractive",
            "--nologo",
            "--debug+",
            "--optimize-",
            "--langversion:preview",
            "--multiemit+",
        };

        args.AddRange(CompileScriptSymbols.For(hostApp).Select(symbol => $"--define:{symbol}"));
        args.AddRange(allReferences.Select(reference => $"--reference:{reference}"));
        return args.ToArray();
    }

    private void FlushOutput(StringBuilder sbOut, StringBuilder sbErr)
    {
        if (sbOut.Length > 0)
        {
            Trace.Write(sbOut.ToString());
            sbOut.Clear();
        }

        if (sbErr.Length <= 0) return;
        logger.ZLogError($"{sbErr.ToString()}");
        sbErr.Clear();
    }

    private void ReportDiagnostics(FSharpDiagnostic[] diagnostics)
    {
        foreach (var diag in diagnostics)
        {
            var msg = ToDiagnosticMessage(diag);
            if (diag.Severity.IsError)
                logger.ZLogError($"{msg}");
            else if (diag.Severity.IsWarning)
                logger.ZLogWarning($"{msg}");
            else
                logger.ZLogDebug($"{msg}");
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
