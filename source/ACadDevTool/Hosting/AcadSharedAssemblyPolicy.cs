using DevTools.Utilities.AssemblyLoading;

namespace AcadDevTool.Hosting;

internal sealed class AcadSharedAssemblyPolicy : IHostSharedAssemblyPolicy
{
    public IReadOnlyCollection<string> HostApiSimpleNames { get; } =
    [
        "acmgd",
        "acdbmgd",
        "accoremgd",
        "acdbmgdbrep",
    ];

    public IReadOnlyCollection<string> HostApiPrefixes { get; } =
    [
        "Autodesk.",
    ];
}
