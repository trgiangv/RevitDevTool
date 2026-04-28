using System.Reflection;
using DevTools.Execution.Interfaces;
using RevitDevTool.Core;

namespace RevitDevTool.HostAdapters;

public sealed class RevitFSharpSupport : IFSharpHostSupport
{
    public IEnumerable<string> GetSessionReferences()
    {
        yield return typeof(IExternalCommand).Assembly.Location;
        var uiAssembly = typeof(Autodesk.Revit.UI.UIApplication).Assembly;
        if (uiAssembly.Location != typeof(IExternalCommand).Assembly.Location)
            yield return uiAssembly.Location;
    }

    public object? FindAndCreateCommand(HashSet<Assembly> assemblySnapshot)
    {
        var current = new HashSet<Assembly>(AppDomain.CurrentDomain.GetAssemblies());
        current.ExceptWith(assemblySnapshot);

        foreach (var assembly in current)
        {
            try
            {
                foreach (var type in assembly.GetTypes())
                {
                    if (type.IsAbstract || type.IsInterface) continue;
                    if (!typeof(IExternalCommand).IsAssignableFrom(type)) continue;
                    return Activator.CreateInstance(type);
                }
            }
            catch { /* skip assemblies that fail reflection */ }
        }

        return null;
    }

    public string GetHostVersion() => RevitContext.Application.VersionNumber;

    public string? GetHostReferencePattern() => @"Revit\s+\d{4}";

    public string GetHostReferenceReplacement() => $"Revit {RevitContext.Application.VersionNumber}";
}
