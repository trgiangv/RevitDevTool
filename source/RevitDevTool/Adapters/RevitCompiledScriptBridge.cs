using System.Reflection;
using System.Text.RegularExpressions;
using DevTools.AssemblyIsolation;
using DevTools.Hosting;

namespace RevitDevTool.Adapters;

public sealed class RevitCompiledScriptBridge(IHostAppInfo hostAppInfo, HostAssemblies hostAssemblies) : ICompiledScriptBridge
{
    private static readonly Regex HostYearRx = new(@"Revit\s+\d{4}", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public IEnumerable<Assembly> GetParentBindings() => hostAssemblies.All();

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

    public string RewriteHostReference(string reference) =>
        HostYearRx.Replace(reference, $"Revit {hostAppInfo.VersionNumber}");
}
