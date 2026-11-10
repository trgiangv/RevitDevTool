using System.Xml.Linq;

namespace DevTools.Update.Autodesk;

/// <summary>
///     Reads and writes <c>PackageContents.xml</c>. The templates in this assembly, one per bundle, list all components that a release can have.
///     A write removes the components that are not selected and keeps all other template data.
/// </summary>
public static class PackageContents
{
    public const string RevitPlatform = "Revit";
    public const string AutoCadPlatform = "AutoCAD*";

    public static IReadOnlyList<string> BundleNames { get; } = ["RevitDevTool.bundle", "AcadDevTool.bundle"];

    private static readonly Lazy<AddinComponent[]> Components = new(() =>
        BundleNames.SelectMany(name => LoadTemplate(name).Root!.Elements("Components").Select(element => ToComponent(name, element))).ToArray());

    public static IReadOnlyList<AddinComponent> TemplateComponents() => Components.Value;

    /// <summary>
    ///     The bundle of a platform, so a host version that the template does not list yet still has one.
    /// </summary>
    public static string BundleOf(AddinTarget target) =>
        Components.Value.FirstOrDefault(component => component.Target.Platform == target.Platform)?.Bundle
        ?? throw new ArgumentException($"No bundle template has the platform {target.Platform}.", nameof(target));

    /// <summary>
    ///     Writes the components of <paramref name="bundle"/> that the user selected and that have their module file on disk.
    ///     Thus a pending or missing payload does not load. Returns the written targets.
    /// </summary>
    public static IReadOnlyList<AddinTarget> Write(AutodeskBundle bundle, IEnumerable<AddinTarget> selection, string version)
    {
        var selected = selection.ToHashSet();
        var document = LoadTemplate(bundle.Name);
        document.Root!.SetAttributeValue("AppVersion", version);

        var components = document.Root.Elements("Components").Select(element => (Element: element, Component: ToComponent(bundle.Name, element))).ToArray();
        foreach (var (element, component) in components)
        {
            if (!selected.Contains(component.Target) || !File.Exists(Path.Combine(bundle.Root, component.ModulePath))) element.Remove();
        }

        Directory.CreateDirectory(bundle.Root);
        var temp = bundle.PackageContentsPath + ".tmp";
        document.Save(temp);
        File.Move(temp, bundle.PackageContentsPath, overwrite: true);
        return components.Where(item => item.Element.Parent is not null).Select(item => item.Component.Target).ToArray();
    }

    public static string ReadVersion(Stream stream) => XDocument.Load(stream).Root!.Attribute("AppVersion")!.Value;

    public static XDocument LoadTemplate(string bundleName)
    {
        using var stream = typeof(PackageContents).Assembly.GetManifestResourceStream($"DevTools.Update.{bundleName}.PackageContents.xml")
                           ?? throw new ArgumentException($"No PackageContents.xml template for {bundleName}.", nameof(bundleName));
        return XDocument.Load(stream);
    }

    private static AddinComponent ToComponent(string bundleName, XElement component)
    {
        var module = component.Element("ComponentEntry")!.Attribute("ModuleName")!.Value.TrimStart('.', '/', '\\');
        var platform = component.Element("RuntimeRequirements")!.Attribute("Platform")!.Value;
        var hostVersion = Path.GetFileName(Path.GetDirectoryName(module))!;
        return new AddinComponent(new AddinTarget(platform, hostVersion), component.Attribute("Description")!.Value, bundleName, module);
    }
}
