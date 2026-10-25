using System.Xml.Linq;

namespace DevTools.Update.Autodesk;

/// <summary>
///     Reads and writes <c>PackageContents.xml</c>. The template in this assembly lists all components that a release can have.
///     A write removes the components that are not selected and keeps all other template data.
/// </summary>
public static class PackageContents
{
    public const string RevitPlatform = "Revit";
    public const string AutoCadPlatform = "AutoCAD*";

    private const string TemplateResource = "DevTools.Update.PackageContents.xml";

    public static IReadOnlyList<AddinComponent> TemplateComponents() => LoadTemplate().Root!.Elements("Components").Select(ToComponent).ToArray();

    /// <summary>
    ///     Writes the components that the user selected and that have their module file on disk.
    ///     Thus a pending or missing payload does not load. Returns the written targets.
    /// </summary>
    public static IReadOnlyList<AddinTarget> Write(AutodeskBundle bundle, IEnumerable<AddinTarget> selection, string version)
    {
        var selected = selection.ToHashSet();
        var document = LoadTemplate();
        document.Root!.SetAttributeValue("AppVersion", version);

        var components = document.Root.Elements("Components").Select(element => (Element: element, Component: ToComponent(element))).ToArray();
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

    private static AddinComponent ToComponent(XElement component)
    {
        var module = component.Element("ComponentEntry")!.Attribute("ModuleName")!.Value.TrimStart('.', '/', '\\');
        var platform = component.Element("RuntimeRequirements")!.Attribute("Platform")!.Value;
        var hostVersion = Path.GetFileName(Path.GetDirectoryName(module))!;
        return new AddinComponent(new AddinTarget(platform, hostVersion), component.Attribute("Description")!.Value, module);
    }

    private static XDocument LoadTemplate()
    {
        using var stream = typeof(PackageContents).Assembly.GetManifestResourceStream(TemplateResource)!;
        return XDocument.Load(stream);
    }
}
