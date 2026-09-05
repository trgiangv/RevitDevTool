using System.IO;

namespace DevTools.Execution.Providers;

/// <summary>
/// FSI compile refs are the on-disk locations of parent bindings.
/// Non-virtual so session ⊆ parent cannot be overridden per host.
/// </summary>
internal static class CompiledScriptBridgeExtensions
{
    public static IEnumerable<string> GetSessionReferences(this ICompiledScriptBridge bridge)
    {
        foreach (var assembly in bridge.GetParentBindings())
        {
            if (assembly.IsDynamic)
                continue;

            string location;
            try
            {
                location = assembly.Location;
            }
            catch
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(location) && File.Exists(location))
                yield return location;
        }
    }
}
