using System.IO;
using System.Reflection;
using DevTools.AssemblyIsolation;
using DevTools.AssemblyIsolation.Diagnostics;
using DevTools.AssemblyIsolation.Loading;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Execution.Providers.Dotnet;
using Microsoft.Extensions.Logging;
using RevitDevTool.Core;
using ZLogger;

namespace RevitDevTool.HostAdapters;

public sealed class RevitCommandRunner(ILogger<RevitCommandRunner> logger) : ICommandRunner
{
    private static ExternalCommandData? _externalCommandData;
    private static ElementSet? _elementSet;

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

    // Command assemblies may show modeless WPF windows that outlive Execute.
    // Collectible unload (NET) or AssemblyResolve unhook (net48) on return
    // tears down chrome / delayed loads while the HWND remains. Keep sessions
    // for the host process lifetime.
    static readonly List<AssemblyIsolationSession> LiveCommandSessions = [];

#if NET
    private ExecutionResult RunInIsolatedContext(CommandItem item)
    {
        var session = AssemblyIsolationSession.Create(
            CommandIsolationPlan.Create(
                item.AssemblyPath,
                RevitHostApis.All(),
                new CommandIsolationDiagnosticSink(logger)));
        LiveCommandSessions.Add(session);
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
                _ => InvokeViaDuckTyping(instance, ref message)
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

    private static Result InvokeViaDuckTyping(object instance, ref string message)
    {
        var method = instance.GetType().GetMethod("Execute");
        object[] parameters = [GetExternalCommandData(), message, GetElementSet()];
        var invocationResult = method?.Invoke(instance, parameters);
        message = (string)parameters[1];
        return invocationResult is Result revitResult ? revitResult : Result.Succeeded;
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
            RevitHostApis.All(),
            new CommandIsolationDiagnosticSink(logger));
        var session = AssemblyIsolationSession.Create(plan);
        LiveCommandSessions.Add(session);
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
        if (_externalCommandData != null)
        {
            _externalCommandData.View = RevitContext.UiApplication.ActiveUIDocument?.ActiveView;
            return _externalCommandData;
        }

        var type = typeof(ExternalCommandData);
        var ctorInfos = type.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        var instance = (ExternalCommandData)ctorInfos[0].Invoke(null);
        instance.Application = RevitContext.UiApplication;
        instance.JournalData ??= new Dictionary<string, string>();
        instance.View = RevitContext.UiApplication.ActiveUIDocument?.ActiveView;
        _externalCommandData = instance;
        return instance;
    }

    private static ElementSet GetElementSet()
    {
        _elementSet ??= new ElementSet();
        if (RevitContext.UiApplication.ActiveUIDocument == null)
        {
            _elementSet.Clear();
            return _elementSet;
        }
        _elementSet.Clear();
        foreach (var elementId in RevitContext.UiApplication.ActiveUIDocument.Selection.GetElementIds())
            _elementSet.Insert(RevitContext.UiApplication.ActiveUIDocument.Document.GetElement(elementId));
        return _elementSet;
    }
}
