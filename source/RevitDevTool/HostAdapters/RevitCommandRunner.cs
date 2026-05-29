using System.IO;
using System.Reflection;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Execution.Providers.Dotnet;
using RevitDevTool.Core;

namespace RevitDevTool.HostAdapters;

public sealed class RevitCommandRunner : ICommandRunner
{
    private static ExternalCommandData? _externalCommandData;
    private static ElementSet? _elementSet;

    public ExecutionResult RunCommand(CommandItem commandItem)
    {
        var message = string.Empty;
        var data = GetExternalCommandData();
        var elements = GetElementSet();

#if NET
        var alc = new CommandLoadContext(commandItem.AssemblyPath);
        try
        {
            var result = ExecuteInIsolatedContext(alc, commandItem, data, ref message, elements);
            return ToExecutionResult(result, message);
        }
        finally
        {
            alc.Unload();
            RevitContext.Application.PurgeReleasedAPIObjects();
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
#else
        var targetDir = Path.GetDirectoryName(commandItem.AssemblyPath)!;
        var loadedNativeHandles = new List<IntPtr>();
        ResolveEventHandler? assemblyResolver = null;
        try
        {
            LoadUnmanagedDependencies(targetDir, ref loadedNativeHandles);
            assemblyResolver = (_, args) => ResolveAssembly(targetDir, args);
            AppDomain.CurrentDomain.AssemblyResolve += assemblyResolver;

            var assemblyBytes = File.ReadAllBytes(commandItem.AssemblyPath);
            var assembly = Assembly.Load(assemblyBytes);
            var instance = assembly.CreateInstance(commandItem.FullClassName);
            if (instance is IExternalCommand command)
            {
                var result = command.Execute(data, ref message, elements);
                return ToExecutionResult(result, message);
            }
            throw new InvalidOperationException(
                $"Failed to create IExternalCommand from '{commandItem.FullClassName}'.");
        }
        finally
        {
            RevitContext.Application.PurgeReleasedAPIObjects();
            if (assemblyResolver != null)
                AppDomain.CurrentDomain.AssemblyResolve -= assemblyResolver;
            foreach (var hModule in loadedNativeHandles)
                while (FreeLibrary(hModule)) { }
        }
#endif
    }

    public ExecutionResult RunCompiledCommand(object compiledCommand)
    {
        if (compiledCommand is not IExternalCommand command)
            return ExecutionResult.Failed($"Compiled type does not implement IExternalCommand.");

        var message = string.Empty;
        var data = GetExternalCommandData();
        var elements = GetElementSet();
        try
        {
            var result = command.Execute(data, ref message, elements);
            return ToExecutionResult(result, message);
        }
        finally
        {
            RevitContext.Application.PurgeReleasedAPIObjects();
        }
    }

#if NET
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static Result ExecuteInIsolatedContext(
        CommandLoadContext alc, CommandItem item, ExternalCommandData data, ref string message, ElementSet elements)
    {
        using var stream = new FileStream(item.AssemblyPath, FileMode.Open, FileAccess.Read);
        var pdbPath = Path.ChangeExtension(item.AssemblyPath, ".pdb");

        Assembly assembly;
        if (File.Exists(pdbPath))
        {
            using var symbolStream = new FileStream(pdbPath, FileMode.Open, FileAccess.Read);
            assembly = alc.LoadFromStream(stream, symbolStream);
        }
        else
        {
            assembly = alc.LoadFromStream(stream);
        }

        var instance = assembly.CreateInstance(item.FullClassName);
        return instance switch
        {
            null => throw new Exception($"Could not create instance of {item.FullClassName}"),
            IExternalCommand command => command.Execute(data, ref message, elements),
            _ => InvokeViaDuckTyping(instance, data, ref message, elements)
        };
    }

    private static Result InvokeViaDuckTyping(object instance, ExternalCommandData data, ref string message, ElementSet elements)
    {
        var method = instance.GetType().GetMethod("Execute");
        object[] parameters = [data, message, elements];
        var invocationResult = method?.Invoke(instance, parameters);
        message = (string)parameters[1];
        return invocationResult is Result revitResult ? revitResult : Result.Succeeded;
    }
#endif

#if NETFRAMEWORK
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
        try
        {
            var assemblyName = new AssemblyName(args.Name);
            var dllPath = Path.Combine(targetDir, assemblyName.Name + ".dll");
            return File.Exists(dllPath) ? Assembly.Load(File.ReadAllBytes(dllPath)) : null;
        }
        catch { return null; }
    }
#endif

    private static ExecutionResult ToExecutionResult(Result result, string message)
    {
        return result switch
        {
            Result.Succeeded => ExecutionResult.Succeeded(message),
            Result.Cancelled => ExecutionResult.Cancelled(message),
            _ => ExecutionResult.Failed(message)
        };
    }

    private static ExternalCommandData GetExternalCommandData()
    {
        if (_externalCommandData != null)
        {
            _externalCommandData.View = RevitContext.UiApplication.ActiveUIDocument?.ActiveView;
            return _externalCommandData;
        }

        var type = typeof(ExternalCommandData);
        var ctors = type.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        var instance = (ExternalCommandData)ctors[0].Invoke(null);
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
