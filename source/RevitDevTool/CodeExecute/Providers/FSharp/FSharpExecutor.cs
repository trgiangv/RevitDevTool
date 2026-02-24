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
/// Compiles and executes F# scripts (.fsx) using FsiEvaluationSession.
/// Scripts define IExternalCommand implementations, executed the same way as DotNet commands.
/// </summary>
internal static class FSharpExecutor
{
    public static IExternalCommand? CompileScript(string scriptPath)
    {
        var assemblySnapshot = new HashSet<Assembly>(AppDomain.CurrentDomain.GetAssemblies());

        var sbOut = new StringBuilder();
        var sbErr = new StringBuilder();
        using var outWriter = new StringWriter(sbOut);
        using var errWriter = new StringWriter(sbErr);

        try
        {
            var argv = BuildSessionArgs();
            var fsiConfig = Shell.FsiEvaluationSession.GetDefaultConfiguration();
            
            // Temporarily switch to the directory containing the assembly with FSharp runtime symbols.
            // https://github.com/dotnet/fsharp/issues/9064
            var prevDir = Environment.CurrentDirectory;
            var fsharpCoreDir = Path.GetDirectoryName(typeof(FSharpOption<>).Assembly.Location);
            if (!string.IsNullOrEmpty(fsharpCoreDir))
                Environment.CurrentDirectory = fsharpCoreDir;

            Shell.FsiEvaluationSession session;
            try
            {
                session = Shell.FsiEvaluationSession.Create(
                    fsiConfig, argv, new StringReader(""), outWriter, errWriter,
                    collectible: FSharpOption<bool>.Some(true),
                    legacyReferenceResolver: null);
            }
            finally
            {
                Environment.CurrentDirectory = prevDir;
            }

            using (session)
            {
                var result = session.EvalScriptNonThrowing(scriptPath);
                var choice = result.Item1;
                // var diagnostics = result.Item2;
                // FlushOutput(sbOut, sbErr);
                // ReportDiagnostics(diagnostics);

                if (choice.IsChoice2Of2)
                {
                    var exn = ((FSharpChoice<Unit, Exception>.Choice2Of2)choice).Item;
                    Trace.TraceError($"F# script evaluation failed: {exn.Message}{Environment.NewLine}{exn.StackTrace}");
                    return null;
                }

                var commandType = FindCommandType(assemblySnapshot);
                if (commandType != null) return (IExternalCommand) Activator.CreateInstance(commandType)!;
                Trace.TraceError("No IExternalCommand with [Transaction] attribute found in F# script.");
                return null;
            }
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
            return null;
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

    private static string[] BuildSessionArgs()
    {
        var args = new List<string>
        {
            "first arg ignored",
            "--noninteractive",
            "--nologo",
#if !NET
            "--multiemit-",
#else
            "--multiemit+",
#endif
        };

        var revitApiLocation = typeof(Document).Assembly.Location;
        if (!string.IsNullOrEmpty(revitApiLocation))
            args.Add($"--reference:{revitApiLocation}");

        var revitApiUiLocation = typeof(UIApplication).Assembly.Location;
        if (!string.IsNullOrEmpty(revitApiUiLocation))
            args.Add($"--reference:{revitApiUiLocation}");
        
        var uiFrameworkLocation = typeof(UIFramework.MainWindow).Assembly.Location;
        if (!string.IsNullOrEmpty(uiFrameworkLocation))
            args.Add($"--reference:{uiFrameworkLocation}");
        
        var adWindowsLocation = typeof(Autodesk.Windows.RibbonButton).Assembly.Location;
        if (!string.IsNullOrEmpty(adWindowsLocation))
            args.Add($"--reference:{adWindowsLocation}");
        
        var traceSourceLocation = typeof(Trace).Assembly.Location;
        if (!string.IsNullOrEmpty(traceSourceLocation))
            args.Add($"--reference:{traceSourceLocation}");

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
                Trace.TraceInformation(msg);
        }
    }

    private static string ToDiagnosticMessage(FSharpDiagnostic diag)
    {
        return $"{diag.FileName}({diag.StartLine},{diag.StartColumn}): {diag.Message}";
    }
}
