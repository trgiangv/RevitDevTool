using DevTools.Utilities.AssemblyLoading;

namespace AcadDevTool.Composition;

internal static class AcadHostApiAssemblies
{
    public static readonly HostApiAssemblySet Set = new(
        ["acmgd", "acdbmgd", "accoremgd", "acdbmgdbrep"],
        ["Autodesk."]);
}
