using System.Reflection;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Runtime;
using DevTools.Execution.Interfaces;
using DevTools.Hosting;

namespace AcadDevTool.HostAdapters;

public sealed class AcadCompiledScriptBridge(IHostAppInfo hostAppInfo) : ICompiledScriptBridge
{
    public IEnumerable<string> GetSessionReferences()
    {
        // acmgd (Runtime — CommandMethodAttribute, ExtensionApplication)
        yield return typeof(CommandMethodAttribute).Assembly.Location;

        // acdbmgd (DatabaseServices — Database, DBObject, Transaction)
        yield return typeof(Database).Assembly.Location;

        // accoremgd (ApplicationServices.Core — Application, DocumentManager)
        yield return typeof(Autodesk.AutoCAD.ApplicationServices.Core.Application).Assembly.Location;
    }

    public Type? TryFindCommandType(Assembly assembly)
    {
        try
        {
            foreach (var type in assembly.GetTypes())
            {
                if (type.IsAbstract || type.IsInterface) continue;
                var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                if (methods.Any(m => m.GetCustomAttributes(typeof(CommandMethodAttribute), false).Length > 0))
                    return type;
            }
        }
        catch { /* skip assemblies that fail reflection */ }

        return null;
    }

    public string GetHostReferencePattern() => @"AutoCAD\s+\d{4}";

    public string GetHostReferenceReplacement() => $"AutoCAD {hostAppInfo.VersionNumber}";
}
