using System.Diagnostics;
using System.IO;
using System.Reflection;
using Autodesk.Revit.UI;
namespace RevitDevTool.Execution.Providers.Dotnet;

internal static class AddinExecutor
{
    public static Result RunCommand(AddinItem addinItem, ExternalCommandData data, ref string message, ElementSet elements)
    {
        var commandResult = Result.Failed;
#if NETCOREAPP
        var filePath = addinItem.AssemblyPath;
        var alc = new AddinLoadContext(filePath);

        try
        {
            commandResult = ExecuteInIsolatedContext(alc, addinItem, data, ref message, elements);
        }
        catch (Exception ex)
        {
            Trace.TraceError($"Execute Error: {ex}");
            commandResult = Result.Failed;
        }
        finally
        {
            alc.Unload();

            Context.Application.PurgeReleasedAPIObjects();
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
#else
        var targetDir = Path.GetDirectoryName(addinItem.AssemblyPath);
        var loadedNativeHandles = new List<IntPtr>();
        ResolveEventHandler? assemblyResolver = null;

        try
        {
            LoadUnmanagedDependencies(targetDir!, ref loadedNativeHandles);

            // dependency resolver
            assemblyResolver = (_, args) =>
            {
                try
                {
                    var assemblyName = new AssemblyName(args.Name);
                    var dllPath = Path.Combine(targetDir!, assemblyName.Name + ".dll");

                    if (File.Exists(dllPath))
                    {
                        return Assembly.Load(File.ReadAllBytes(dllPath));
                    }
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning($"Failed to resolve dependency {args.Name}: {ex.Message}");
                }
                return null;
            };

            AppDomain.CurrentDomain.AssemblyResolve += assemblyResolver;

            commandResult = ExecuteInCurrentAppDomain(addinItem, data, ref message, elements);
        }
        catch (Exception ex)
        {
            Trace.TraceError($"NetFramework Load Error: {ex}");
            commandResult = Result.Failed;
        }
        finally
        {
            if (assemblyResolver != null)
            {
                AppDomain.CurrentDomain.AssemblyResolve -= assemblyResolver;
            }

            foreach (var hModule in loadedNativeHandles)
            {
                while (FreeLibrary(hModule)) { }
                Debug.WriteLine("[AddinManager] Released Native DLL handle.");
            }
        }
#endif
        return commandResult;
    }

#if NETFRAMEWORK
    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
    private static extern IntPtr LoadLibrary(string lpFileName);
    
    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeLibrary(IntPtr hModule);
    
    private static void LoadUnmanagedDependencies(string directoryPath, ref List<IntPtr> loadedHandles)
    {
        var dllFiles = Directory.GetFiles(directoryPath, "*.dll");
        foreach (var file in dllFiles)
        {
            if (Utils.AssemblyLoader.IsManagedAssembly(file)) continue;
            var hModule = LoadLibrary(file);
            if (hModule == IntPtr.Zero) continue;
            loadedHandles.Add(hModule);
            Debug.WriteLine($"[AddinManager] Loaded Native DLL: {Path.GetFileName(file)}");
        }
    }
    
    private static Result ExecuteInCurrentAppDomain(
        AddinItem addinItem, 
        ExternalCommandData data, 
        ref string message, 
        ElementSet elements)
    {
        var assemblyBytes = File.ReadAllBytes(addinItem.AssemblyPath);
        var assembly = Assembly.Load(assemblyBytes);
        var instance = assembly.CreateInstance(addinItem.FullClassName);

        if (instance is IExternalCommand externalCommand)
        {
            return externalCommand.Execute(data, ref message, elements);
        }

        Trace.TraceError($"Failed to create instance of {addinItem.FullClassName}. Instance type: {instance?.GetType().FullName ?? "null"}");
        return Result.Failed;
    }
#endif

#if NETCOREAPP
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static Result ExecuteInIsolatedContext(AddinLoadContext alc, AddinItem item, ExternalCommandData data, ref string message, ElementSet elements)
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
        switch (instance)
        {
            case null:
                throw new Exception($"Could not create instance of {item.FullClassName}");
            case IExternalCommand command:
                return command.Execute(data, ref message, elements);
            default:
            {
                var method = instance.GetType().GetMethod("Execute");
                object[] parameters = [data, message, elements];
                var invocationResult = method?.Invoke(instance, parameters);
                message = (string) parameters[1];

                return invocationResult switch
                {
                    Result revitResult => revitResult,
                    _ => Result.Succeeded
                };
            }
        }
    }
#endif
}