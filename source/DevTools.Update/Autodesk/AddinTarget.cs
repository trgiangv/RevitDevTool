namespace DevTools.Update.Autodesk;

/// <summary>
///     One <c>Components</c> entry of <c>PackageContents.xml</c>: an Autodesk platform (<c>Revit</c>, <c>AutoCAD*</c>)
///     at one host version. The host version is also the name of the payload folder.
/// </summary>
public sealed record AddinTarget(string Platform, string HostVersion);

/// <summary>
///     An <see cref="AddinTarget"/> with the data from its template component.
/// </summary>
public sealed record AddinComponent(AddinTarget Target, string Description, string ModulePath);
