namespace DevTools.Update.Autodesk;

/// <summary>
///     Paths in <c>RevitDevTool.bundle</c>. Each payload is a folder <c>Contents\&lt;host version&gt;</c>.
///     <c>.updates</c> is on the same volume as <c>Contents</c>. Thus staging and rollback moves are renames (0049).
/// </summary>
public sealed class AutodeskBundle(string root)
{
    public const string BundleName = "RevitDevTool.bundle";

    public string Root { get; } = root;
    public string Contents => Path.Combine(Root, "Contents");
    public string Updates => Path.Combine(Root, ".updates");
    public string Rollback => Path.Combine(Updates, "rollback");
    public string InstallationPath => Path.Combine(Root, "installation.json");
    public string PackageContentsPath => Path.Combine(Root, "PackageContents.xml");

    public string PayloadPath(string hostVersion) => Path.Combine(Contents, hostVersion);

    public string StagingPath(string version) => Path.Combine(Updates, version);

    public string RollbackPath(string hostVersion) => Path.Combine(Rollback, hostVersion);

    public static string DefaultRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Autodesk", "ApplicationPlugins", BundleName);
}
