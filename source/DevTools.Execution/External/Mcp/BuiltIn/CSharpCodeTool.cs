using System.ComponentModel;
using System.Runtime.CompilerServices;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Execution.Providers.CSharp;
using DevTools.Hosting;
using DevTools.Mcp;
using DevTools.Mcp.Core.Protocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace DevTools.Execution.External.Mcp.BuiltIn;

/// <summary>Compiles and executes C# code in the host process via Roslyn.</summary>
public sealed class CSharpCodeTool : IBuiltInMcpTool
{
    private static readonly TimeSpan CompileTimeout = TimeSpan.FromSeconds(30);

    private readonly ICompiledScriptBridge _scriptBridge;
    private readonly CSharpCompiler _compiler;
    private readonly IHostContextExecutor _hostContext;
    private readonly ICommandRunner _commandRunner;

    public CSharpCodeTool(
        ICompiledScriptBridge scriptBridge,
        CSharpCompiler compiler,
        IHostContextExecutor hostContext,
        ICommandRunner commandRunner,
        IHostAppInfo hostApp)
    {
        _scriptBridge = scriptBridge;
        _compiler = compiler;
        _hostContext = hostContext;
        _commandRunner = commandRunner;
        ServerTool = McpServerTool.Create(
            ExecuteAsync,
            new McpServerToolCreateOptions
            {
                Name = McpSpecKeys.Tool.ExecuteCSharp,
                Title = "Execute C# Code",
                Description = DescribeTool(hostApp.Host),
                Destructive = true,
                OpenWorld = true
            });
    }

    public string Name => McpSpecKeys.Tool.ExecuteCSharp;
    public McpServerTool ServerTool { get; }

    [Description("Compile and execute C# code in the running host process.")]
    private async Task<CallToolResult> ExecuteAsync(
        [Description("Complete C# source for this host command entry point.")]
        string code,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            return ToolHelpers.ErrorResult($"{McpSpecKeys.Result.Compilation} Code parameter must not be empty.");

        ScriptCompilationResult? compilationResult = null;
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(CompileTimeout);

            try
            {
                compilationResult = await _compiler
                    .CompileAsync(code, _scriptBridge, ct: timeoutCts.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return ToolHelpers.ErrorResult($"{McpSpecKeys.Result.Compilation} Timed out after {CompileTimeout.TotalSeconds}s. " +
                    "Simplify code or reduce #r nuget dependencies.");
            }

            if (!compilationResult.Success || compilationResult.Command is null)
            {
                var diagnostics = compilationResult.FormatDiagnostics();
                return ToolHelpers.ErrorResult($"{McpSpecKeys.Result.Compilation} Fix the code and retry.\n{diagnostics}");
            }

            var result = await _hostContext
                .ExecuteAsync(() => _commandRunner.RunCompiledCommand(compilationResult.Command), cancellationToken)
                .ConfigureAwait(false);

            if (!result.Success)
            {
                var error = result.Message;
                var prefix = error.Contains("rolled back", StringComparison.OrdinalIgnoreCase)
                    ? $"{McpSpecKeys.Result.Rollback} Transaction failed due to unresolvable constraint.\n"
                    : $"{McpSpecKeys.Result.Runtime} ";
                return ToolHelpers.ErrorResult($"{prefix}{error}");
            }

            var output = result.Message;
            var rollback = ExecutionGuardContext.RollbackSummary;
            if (!string.IsNullOrEmpty(rollback))
                output = $"{output}\n\n {rollback}";

            return ToolHelpers.Result(output);
        }
        finally
        {
            DisposeCompilation(compilationResult);
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void DisposeCompilation(ScriptCompilationResult? result)
    {
        result?.Cleanup?.Dispose();

#if NET
        GC.Collect();
        GC.WaitForPendingFinalizers();
#endif
    }

    private static string DescribeTool(HostApp host) => host switch
    {
        HostApp.Revit =>
            """
            Compile and execute one public IExternalCommand in Revit. Send this type, not a snippet. The host does not wrap a snippet.

            using System;
            using System.Collections.Generic;
            using System.Linq;
            using Autodesk.Revit.DB;
            using Autodesk.Revit.UI;
            using Autodesk.Revit.Attributes;

            [Transaction(TransactionMode.Manual)]
            public class Command : IExternalCommand
            {
                public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
                {
                    var doc = commandData.Application.ActiveUIDocument.Document;
                    message = "result text";
                    return Result.Succeeded;
                }
            }

            Open your own Transaction for a write. Internal units are feet. Put caller-visible text in message. Use #r and #r "nuget:" for extra assemblies.
            """ + $" For a Python script, use {McpSpecKeys.Tool.ExecutePython}. Errors: {McpSpecKeys.Result.Compilation} fix the code, {McpSpecKeys.Result.Runtime} check logic, {McpSpecKeys.Result.Rollback} constraint violation.",
        _ when host.IsAcadFamily() =>
            """
            Compile and execute one public [CommandMethod] in AutoCAD. Send this type, not a snippet. The host does not wrap a snippet.

            using Autodesk.AutoCAD.ApplicationServices;
            using Autodesk.AutoCAD.DatabaseServices;
            using Autodesk.AutoCAD.EditorInput;
            using Autodesk.AutoCAD.Runtime;

            public class Command
            {
                [CommandMethod("MYCOMMAND", CommandFlags.Session)]
                public void Execute()
                {
                    var doc = Application.DocumentManager.MdiActiveDocument;
                    using (doc.LockDocument())
                    using (var tr = doc.Database.TransactionManager.StartTransaction())
                    {
                        tr.Commit();
                    }
                    doc.Editor.WriteMessage("\nResult text");
                }
            }

            CommandFlags.Session and doc.LockDocument() are required. Without them the call fails because a different thread owns the document. Use #r and #r "nuget:" for extra assemblies.
            """ + $" For a Python script, use {McpSpecKeys.Tool.ExecutePython}. Errors: {McpSpecKeys.Result.Compilation} fix the code, {McpSpecKeys.Result.Runtime} check logic, {McpSpecKeys.Result.Rollback} constraint violation.",
        _ =>
            "Compile and execute C# in the host process. Send the host command entry type, not a snippet. " +
            $"For a Python script, use {McpSpecKeys.Tool.ExecutePython}. " +
            "Use #r for extra assemblies and #r \"nuget:\" for packages."
    };
}
