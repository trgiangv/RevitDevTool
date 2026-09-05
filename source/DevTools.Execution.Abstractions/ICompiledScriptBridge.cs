using System.Reflection;

namespace DevTools.Execution.Abstractions;

/// <summary>
/// Host-specific support for script compilation (C# and F#): parent identity for
/// isolated loads, command-type discovery, and host-year #r rewriting.
/// </summary>
public interface ICompiledScriptBridge
{
    /// <summary>
    /// Returns the already-loaded host contract assemblies that compiled scripts must share
    /// with the host rather than load privately.
    /// </summary>
    IEnumerable<Assembly> GetParentBindings();

    /// <summary>
    /// Tries to find the command type in the given assembly. Returns null if not found or if the assembly can't be reflected.
    /// </summary>
    Type? TryFindCommandType(Assembly assembly);

    /// <summary>
    /// Rewrites a host-versioned assembly path in a #r directive (e.g. Revit 2024 → Revit 2025).
    /// Returns <paramref name="reference"/> unchanged when it is not a host-year path.
    /// </summary>
    string RewriteHostReference(string reference);
}
