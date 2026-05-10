using RevitDevTool.Core;
using RevitDevTool.Core.DockableLoader;

namespace RevitDevTool.ExternalEvent.App;

/// <summary>
///     Holds the active <see cref="CollectibleAssemblyPane" /> so ribbon commands and in-pane UI can call
///     <see cref="CollectibleAssemblyPane.Reload" />.
/// </summary>
internal static class HotReloadPaneSession
{
    public static CollectibleAssemblyPane? Host { get; private set; }

    private static string? _assemblyPath;
    private static string? _typeName;

    public static void Attach(CollectibleAssemblyPane host, string assemblyPath, string typeName)
    {
        Host = host;
        _assemblyPath = assemblyPath;
        _typeName = typeName;
    }

    public static void Reload()
    {
        if (Host is null || string.IsNullOrEmpty(_assemblyPath) || string.IsNullOrEmpty(_typeName))
            return;

        Host.Reload(_assemblyPath, _typeName);
    }
}
