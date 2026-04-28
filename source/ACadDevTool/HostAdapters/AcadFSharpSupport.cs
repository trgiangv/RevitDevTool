using System.Reflection;
using Autodesk.AutoCAD.Runtime;
using DevTools.Execution.Interfaces;
using DevTools.Utilities;

namespace AcadDevTool.HostAdapters;

public sealed class AcadFSharpSupport : IFSharpHostSupport
{
    public IEnumerable<string> GetSessionReferences()
    {
        yield return typeof(CommandMethodAttribute).Assembly.Location;
    }

    public object? FindAndCreateCommand(HashSet<Assembly> assemblySnapshot)
    {
        var current = new HashSet<Assembly>(AppDomain.CurrentDomain.GetAssemblies());
        current.ExceptWith(assemblySnapshot);

        foreach (var assembly in current)
        {
            var commandType = TryFindCommandType(assembly);
            if (commandType != null)
                return Activator.CreateInstance(commandType);
        }

        return null;
    }

    private static Type? TryFindCommandType(Assembly assembly)
    {
        try
        {
            foreach (var type in assembly.GetTypes())
            {
                if (type.IsAbstract || type.IsInterface) continue;
                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                if (methods.Any(m => m.GetCustomAttributes(typeof(CommandMethodAttribute), false).Length > 0))
                    return type;
            }
        }
        catch { /* skip assemblies that fail reflection */ }

        return null;
    }

    public string GetHostVersion()
    {
        return SettingsUtils.AutodeskVersion;
    }

    public string GetHostReferencePattern() => @"AutoCAD\s+\d{4}";

    public string GetHostReferenceReplacement()
    {
        return $"AutoCAD {SettingsUtils.AutodeskVersion}";
    }
}
