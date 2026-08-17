using System.IO;
using System.Reflection;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Execution.Providers.Dotnet;
using DevTools.Utilities.AssemblyLoading;
using RevitDevTool.Core;

namespace RevitDevTool.HostAdapters;

public sealed class RevitCommandRunner : ICommandRunner
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

#if NET
    private static ExecutionResult RunInIsolatedContext(CommandItem item)
    {
        var alc = new CommandLoadContext(item.AssemblyPath);
        try
        {
            return LoadAndExecute(alc, item);
        }
        finally
        {
            alc.Unload();
            RevitContext.Application.PurgeReleasedAPIObjects();
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static ExecutionResult LoadAndExecute(CommandLoadContext alc, CommandItem item)
    {
        var assembly = LoadAssemblyWithSymbols(alc, item.AssemblyPath);
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

    private static Assembly LoadAssemblyWithSymbols(CommandLoadContext alc, string assemblyPath) =>
        ByteAssemblyLoader.LoadFromFileStream(alc, assemblyPath);

    private static Result InvokeViaDuckTyping(object instance, ref string message)
    {
        var method = instance.GetType().GetMethod("Execute");
        object[] parameters = [GetExternalCommandData(), message, GetElementSet()];
        var invocationResult = method?.Invoke(instance, parameters);
        message = (string)parameters[1];
        return invocationResult is Result revitResult ? revitResult : Result.Succeeded;
    }
#endif

#if NETFRAMEWORK
    private static ExecutionResult RunInAppDomain(CommandItem item)
    {
        var targetDir = Path.GetDirectoryName(item.AssemblyPath)!;
        var loadedNativeHandles = new List<IntPtr>();
        ResolveEventHandler? assemblyResolver = null;
        try
        {
            LoadUnmanagedDependencies(targetDir, ref loadedNativeHandles);
            assemblyResolver = (_, args) => ResolveAssembly(targetDir, args);
            AppDomain.CurrentDomain.AssemblyResolve += assemblyResolver;

            var command = LoadCommand(item);
            return ExecuteCommand(command);
        }
        finally
        {
            RevitContext.Application.PurgeReleasedAPIObjects();
            if (assemblyResolver != null)
                AppDomain.CurrentDomain.AssemblyResolve -= assemblyResolver;
            foreach (var hModule in loadedNativeHandles)
                while (FreeLibrary(hModule)) { }
        }
    }

    private static IExternalCommand LoadCommand(CommandItem item)
    {
        var assembly = ByteAssemblyLoader.LoadFromFile(item.AssemblyPath);
        var instance = assembly.CreateInstance(item.FullClassName);
        return instance as IExternalCommand
            ?? throw new InvalidOperationException($"Failed to create IExternalCommand from '{item.FullClassName}'.");
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
    private static extern IntPtr LoadLibrary(string lpFileName);

    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeLibrary(IntPtr hModule);

    private static void LoadUnmanagedDependencies(string directoryPath, ref List<IntPtr> loadedHandles)
    {
        foreach (var file in Directory.GetFiles(directoryPath, "*.dll"))
        {
            if (DevTools.Utilities.AssemblyLoader.IsManagedAssembly(file)) continue;
            var hModule = LoadLibrary(file);
            if (hModule != IntPtr.Zero) loadedHandles.Add(hModule);
        }
    }

    private static Assembly? ResolveAssembly(string targetDir, ResolveEventArgs args)
    {
        if (args.Name is null)
            return null;

        return DirectoryAssemblyLoader.TryLoad(targetDir, new AssemblyName(args.Name));
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
