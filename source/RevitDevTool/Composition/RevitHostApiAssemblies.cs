using DevTools.Utilities.AssemblyLoading;

namespace RevitDevTool.Composition;

internal static class RevitHostApiAssemblies
{
    public static readonly HostSharedAssemblyNames Names = new(
        ["RevitAPI", "RevitAPIUI", "AdWindows"],
        ["Autodesk."]);
}
