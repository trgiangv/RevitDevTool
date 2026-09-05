using System.Reflection;
using System.Text.RegularExpressions;
using Autodesk.AutoCAD.Runtime;
using DevTools.AssemblyIsolation;
using DevTools.Hosting;

namespace AcadDevTool.Adapters;

public sealed class AcadCompiledScriptBridge(IHostAppInfo hostAppInfo, HostAssemblies hostAssemblies) : ICompiledScriptBridge
{
    private static readonly Regex HostYearRx = new(@"AutoCAD\s+\d{4}", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public IEnumerable<Assembly> GetParentBindings() => hostAssemblies.All();

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

    public string RewriteHostReference(string reference) =>
        HostYearRx.Replace(reference, $"AutoCAD {hostAppInfo.VersionNumber}");
}
