using System.Reflection;
using DevTools.Execution.Interfaces;
using DevTools.Hosting;

namespace RevitDevTool.HostAdapters;

public sealed class RevitCompiledScriptBridge(IHostAppInfo hostAppInfo) : ICompiledScriptBridge
{
    public IEnumerable<string> GetSessionReferences()
    {
        yield return typeof(IExternalCommand).Assembly.Location;
        var uiAssembly = typeof(UIApplication).Assembly;
        if (uiAssembly.Location != typeof(IExternalCommand).Assembly.Location)
            yield return uiAssembly.Location;
    }

    public Type? TryFindCommandType(Assembly assembly)
    {
        try
        {
            foreach (var type in assembly.GetTypes())
            {
                if (type.IsAbstract || type.IsInterface) continue;
                if (!typeof(IExternalCommand).IsAssignableFrom(type)) continue;

                return type;
            }
        }
        catch { /* skip assemblies that fail reflection */ }

        return null;
    }

    public string GetHostReferencePattern() => @"Revit\s+\d{4}";

    public string GetHostReferenceReplacement() => $"Revit {hostAppInfo.VersionNumber}";
}
