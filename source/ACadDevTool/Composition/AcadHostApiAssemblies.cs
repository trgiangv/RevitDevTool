using DevTools.Utilities.AssemblyLoading;

namespace AcadDevTool.Composition;

internal static class AcadHostApiAssemblies
{
    public static readonly HostSharedAssemblyNames Names = new(
        ["acmgd", "acdbmgd", "accoremgd", "acdbmgdbrep"],
        ["Autodesk."]);
}
