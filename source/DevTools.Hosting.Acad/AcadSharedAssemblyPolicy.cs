namespace DevTools.Hosting.Acad;

public sealed class AcadSharedAssemblyPolicy : IHostSharedAssemblyPolicy
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
