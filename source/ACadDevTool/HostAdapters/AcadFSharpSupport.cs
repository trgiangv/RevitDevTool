using System.Reflection;
using Autodesk.AutoCAD.Runtime;
using DevTools.Execution.Interfaces;
using DevTools.Logging;

namespace AcadDevTool.HostAdapters;

public sealed class AcadFSharpSupport(IHostAppInfo hostAppInfo) : IFSharpHostSupport
{
    public IEnumerable<string> GetSessionReferences()
    {
        yield return typeof(CommandMethodAttribute).Assembly.Location;
    }

    public Type? TryFindCommandType(Assembly assembly)
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

    public string GetHostVersion() => hostAppInfo.VersionNumber;

    public string GetHostReferencePattern() => @"AutoCAD\s+\d{4}";

    public string GetHostReferenceReplacement() => $"AutoCAD {hostAppInfo.VersionNumber}";
}
