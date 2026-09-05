#if !NET
using System.IO;
using DevTools.AssemblyIsolation.Loading;
#endif
using System.Reflection;
using DevTools.AssemblyIsolation;
using DevTools.AssemblyIsolation.Diagnostics;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Execution.Providers.Dotnet;
using Microsoft.Extensions.Logging;
using RevitDevTool.Core;
using ZLogger;
namespace RevitDevTool.Adapters;

public sealed class RevitCommandRunner(ILogger<RevitCommandRunner> logger, HostAssemblies hostAssemblies) : ICommandRunner
{
    private static ExternalCommandData? externalCommandData;
    private static ElementSet? elementSet;

    public ExecutionResult RunCommand(CommandItem commandItem)
    {
#if NET
        return RunInIsolatedContext(commandItem);
#else
        return RunInAppDomain(commandItem);
#endif
    }

    public ExecutionResult RunCompiledCommand(object compiledCommand)
    {
        if (compiledCommand is not IExternalCommand command)
            return ExecutionResult.Failed("Compiled type does not implement IExternalCommand.");

        try
        {
            return ExecuteCommand(command);
        }
        finally
        {
            RevitContext.Application.PurgeReleasedAPIObjects();
        }
    }

    private static ExecutionResult ExecuteCommand(IExternalCommand command)
    {
        var message = string.Empty;
        try
        {
            var result = command.Execute(GetExternalCommandData(), ref message, GetElementSet());
            return ToExecutionResult(result, message);
        }
        catch (Exception ex)
        {
            return ExecutionResult.Failed(
                !string.IsNullOrEmpty(message) ? message : ex.Message,
                ex);
        }
    }

#if NET
    private ExecutionResult RunInIsolatedContext(CommandItem item)
    {
        var session = AssemblyIsolationSession.Create(
            CommandIsolationPlan.Create(
                item.AssemblyPath,
                hostAssemblies.All(),
                new CommandIsolationDiagnosticSink(logger)));
        try
        {
            return LoadAndExecute(session, item);
        }
        finally
        {
            RevitContext.Application.PurgeReleasedAPIObjects();
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ExecutionResult LoadAndExecute(AssemblyIsolationSession session, CommandItem item)
    {
        var assembly = session.LoadEntryAssembly();
        var instance = assembly.CreateInstance(item.FullClassName)
            ?? throw new InvalidOperationException($"Could not create instance of '{item.FullClassName}'.");

        var message = string.Empty;
        try
        {
            var result = instance switch
            {
                IExternalCommand command => command.Execute(
                    GetExternalCommandData(), ref message, GetElementSet()),
                _ => throw new InvalidOperationException($"Failed to create IExternalCommand from '{item.FullClassName}'.")
            };
            return ToExecutionResult(result, message);
        }
        catch (Exception ex)
        {
            return ExecutionResult.Failed(
                !string.IsNullOrEmpty(message) ? message : ex.Message,
                ex);
        }
    }
#endif

    private sealed class CommandIsolationDiagnosticSink(ILogger logger) : IAssemblyIsolationDiagnosticSink
    {
        public void Publish(AssemblyIsolationDiagnostic diagnostic) => logger.ZLogDebug(
            $"[RevitCommandRunner] Assembly isolation diagnostic '{diagnostic.Code}': {diagnostic.Message}");
    }

#if NETFRAMEWORK
    private ExecutionResult RunInAppDomain(CommandItem item)
    {
        var plan = CommandIsolationPlan.Create(
            item.AssemblyPath,
            hostAssemblies.All(),
            new CommandIsolationDiagnosticSink(logger));
        var session = AssemblyIsolationSession.Create(plan);
        try
        {
            NativeLibraryPreloader.LoadUnmanagedFromDirectory(Path.GetDirectoryName(item.AssemblyPath)!);
            return LoadAndExecute(session, item);
        }
        finally
        {
            RevitContext.Application.PurgeReleasedAPIObjects();
        }
    }

    private static ExecutionResult LoadAndExecute(AssemblyIsolationSession session, CommandItem item)
    {
        var assembly = session.LoadEntryAssembly();
        var instance = assembly.CreateInstance(item.FullClassName)
            ?? throw new InvalidOperationException($"Could not create instance of '{item.FullClassName}'.");
        return instance is IExternalCommand command
            ? ExecuteCommand(command)
            : throw new InvalidOperationException($"Failed to create IExternalCommand from '{item.FullClassName}'.");
    }
#endif

    private static ExecutionResult ToExecutionResult(Result result, string message) => result switch
    {
        Result.Succeeded => ExecutionResult.Succeeded(message),
        Result.Cancelled => ExecutionResult.Cancelled(message),
        _ => ExecutionResult.Failed(message)
    };

    private static ExternalCommandData GetExternalCommandData()
    {
        if (externalCommandData != null)
        {
            externalCommandData.View = RevitContext.UiApplication.ActiveUIDocument?.ActiveView;
            return externalCommandData;
        }

        var type = typeof(ExternalCommandData);
        var ctorInfos = type.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        var instance = (ExternalCommandData)ctorInfos[0].Invoke(null);
        instance.Application = RevitContext.UiApplication;
        instance.JournalData ??= new Dictionary<string, string>();
        instance.View = RevitContext.UiApplication.ActiveUIDocument?.ActiveView;
        externalCommandData = instance;
        return instance;
    }

    private static ElementSet GetElementSet()
    {
        elementSet ??= new ElementSet();
        if (RevitContext.UiApplication.ActiveUIDocument == null)
        {
            elementSet.Clear();
            return elementSet;
        }
        elementSet.Clear();
        foreach (var elementId in RevitContext.UiApplication.ActiveUIDocument.Selection.GetElementIds())
            elementSet.Insert(RevitContext.UiApplication.ActiveUIDocument.Document.GetElement(elementId));
        return elementSet;
    }
}
