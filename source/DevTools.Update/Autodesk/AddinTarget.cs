namespace DevTools.Update.Autodesk;

/// <summary>
///     One <c>Components</c> entry of <c>PackageContents.xml</c>: an Autodesk platform (<c>Revit</c>, <c>AutoCAD*</c>)
///     at one host version. It is also the payload unit: one folder <c>Contents\&lt;host version&gt;</c> of its bundle.
/// </summary>
public sealed record AddinTarget(string Platform, string HostVersion)
{
    public override string ToString() => $"{Platform.TrimEnd('*')} {HostVersion}";
}

/// <summary>
///     An <see cref="AddinTarget"/> with the data from its template component. <see cref="ModulePath"/> is relative to <see cref="Bundle"/>.
/// </summary>
public sealed record AddinComponent(AddinTarget Target, string Description, string Bundle, string ModulePath);
