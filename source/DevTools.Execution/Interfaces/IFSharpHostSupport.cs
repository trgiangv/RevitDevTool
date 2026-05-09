using System.Reflection;

namespace DevTools.Execution.Interfaces;

/// <summary>
/// Host-specific support for F# script compilation: provides host API assembly references 
/// and finds the compiled command type after FSI evaluation.
/// </summary>
public interface IFSharpHostSupport
{
    /// <summary>
    /// Returns assembly paths for host API references (e.g., RevitAPI.dll, RevitAPIUI.dll)
    /// to add to the FSI session.
    /// </summary>
    IEnumerable<string> GetSessionReferences();

    /// <summary>
    /// Tries to find the command type in the given assembly. Returns null if not found or if the assembly can't be reflected.
    /// </summary>
    /// <param name="assembly">The assembly to search for the command type.</param>
    /// <returns>The command type if found; null otherwise.</returns>
    Type? TryFindCommandType(Assembly assembly);

    /// <summary>
    /// Returns the host version string for correcting assembly references (e.g., "2025" for Revit).
    /// </summary>
    string GetHostVersion();

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
