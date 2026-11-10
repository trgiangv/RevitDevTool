namespace DevTools.Update.Autodesk;

/// <summary>
///     Paths in one bundle (<c>RevitDevTool.bundle</c>, <c>AcadDevTool.bundle</c>). Each payload is a folder <c>Contents\&lt;host version&gt;</c>.
///     <c>.updates</c> is on the same volume as <c>Contents</c>. Thus staging and rollback moves are renames (0049).
/// </summary>
public sealed class AutodeskBundle(string root)
{
    public string Root { get; } = root;
    public string Name => Path.GetFileName(Root);
    public string Contents => Path.Combine(Root, "Contents");
    public string Updates => Path.Combine(Root, ".updates");
    public string Rollback => Path.Combine(Updates, "rollback");
    public string PackageContentsPath => Path.Combine(Root, "PackageContents.xml");

    public string PayloadPath(string hostVersion) => Path.Combine(Contents, hostVersion);

    public string StagingPath(string version) => Path.Combine(Updates, version);

    public string RollbackPath(string hostVersion) => Path.Combine(Rollback, hostVersion);
}

/// <summary>
///     The bundles of the template in one <c>ApplicationPlugins</c> folder. A target belongs to the bundle whose template lists it.
/// </summary>
public sealed class AutodeskBundles(string root)
{
    public string Root { get; } = root;

    public IReadOnlyList<AutodeskBundle> All => PackageContents.BundleNames.Select(Named).ToArray();

    public AutodeskBundle Named(string name) => new(Path.Combine(Root, name));

    public AutodeskBundle For(AddinTarget target) => Named(PackageContents.BundleOf(target));

    public string PayloadPath(AddinTarget target) => For(target).PayloadPath(target.HostVersion);

    public static string DefaultRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Autodesk", "ApplicationPlugins");
}
