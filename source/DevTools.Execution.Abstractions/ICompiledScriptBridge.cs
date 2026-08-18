using System.Reflection;

namespace DevTools.Execution.Abstractions;

/// <summary>
/// Host-specific support for script compilation (C# and F#): provides host API assembly references,
/// reference rewriting for host versions, and command type discovery in compiled assemblies.
/// </summary>
public interface ICompiledScriptBridge
{
    /// <summary>
    /// Returns assembly paths for host API references (e.g., RevitAPI.dll, RevitAPIUI.dll)
    /// to add to the compilation session.
    /// </summary>
    IEnumerable<string> GetSessionReferences();

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
    /// Returns a regex pattern matching host-versioned assembly references in #r directives.
    /// E.g. <c>Revit\s+\d{4}</c> for Revit, <c>AutoCAD\s+\d{4}</c> for AutoCAD.
    /// Return null if no reference rewriting is needed.
    /// </summary>
    string? GetHostReferencePattern();

    /// <summary>
    /// Returns the replacement text for host reference rewriting, e.g. "Revit 2025".
    /// </summary>
    string GetHostReferenceReplacement();
}
