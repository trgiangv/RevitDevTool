namespace DevTools.Hosting.Revit;

public sealed class RevitSharedAssemblyPolicy : IHostSharedAssemblyPolicy
{
    public IReadOnlyCollection<string> HostApiSimpleNames { get; } =
    [
        "RevitAPI",
        "RevitAPIUI",
        "AdWindows",
    ];

    public IReadOnlyCollection<string> HostApiPrefixes { get; } =
    [
        "Autodesk.",
    ];
}
