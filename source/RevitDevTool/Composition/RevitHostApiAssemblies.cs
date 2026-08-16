using DevTools.Utilities.AssemblyLoading;

namespace RevitDevTool.Composition;

internal static class RevitHostApiAssemblies
{
    public static readonly HostApiAssemblySet Set = new(
        ["RevitAPI", "RevitAPIUI", "AdWindows"],
        ["Autodesk."]);
}
