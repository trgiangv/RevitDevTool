using System.Xml.Linq;

namespace Build.Products;

/// <summary>
///     The <c>ProjectReference</c> graph read from the csproj files. Paths are repo-relative with <c>/</c> and the casing on disk.
/// </summary>
public static class ProjectGraph
{
    public static IReadOnlyList<string> Closure(string root, IEnumerable<string> projects)
    {
        var seen = new SortedSet<string>(StringComparer.Ordinal);
        var pending = new Stack<string>(projects.Select(Normalize));
        while (pending.TryPop(out var project))
        {
            if (!seen.Add(project)) continue;
            if (!File.Exists(Path.Combine(root, project))) throw new InvalidOperationException($"{project} is missing; fix the product roots or the ProjectReference that names it.");

            foreach (var reference in References(root, project)) pending.Push(reference);
        }

        return seen.ToArray();
    }

    public static IReadOnlyList<string> TestProjects(string root, Product product) => TestProjects(root, Closure(root, product.Roots));

    /// <summary>
    ///     <c>tests/X/X.csproj</c> projects that reference a project of <paramref name="closure"/> or the build tool.
    /// </summary>
    public static IReadOnlyList<string> TestProjects(string root, IReadOnlyCollection<string> closure)
    {
        var tests = Path.Combine(root, "tests");
        if (!Directory.Exists(tests)) return [];

        return Directory.GetDirectories(tests)
            .Select(folder => Path.Combine(folder, Path.GetFileName(folder) + ".csproj"))
            .Where(project => File.Exists(project) && project.EndsWith(".Tests.csproj", StringComparison.Ordinal))
            .Select(project => Normalize(Path.GetRelativePath(root, project)))
            .Where(project => References(root, project).Any(reference => closure.Contains(reference) || reference.StartsWith("build/", StringComparison.Ordinal)))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    public static bool NeedsHostYear(string root, string project) =>
        Closure(root, [project]).Any(item => Elements(root, item, "UseRevit", "UseAutoCad").Any(value => value == "true"));

    public static IReadOnlyList<string> TargetFrameworks(string root, string project) =>
        Elements(root, project, "TargetFramework", "TargetFrameworks").FirstOrDefault()?
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        ?? throw new InvalidOperationException($"{project} declares no TargetFramework; add one.");

    private static IEnumerable<string> References(string root, string project)
    {
        var folder = Path.GetDirectoryName(Path.Combine(root, project))!;
        return XDocument.Load(Path.Combine(root, project))
            .Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .OfType<string>()
            .Select(include => OnDisk(root, Path.GetFullPath(Path.Combine(folder, include.Replace('\\', Path.DirectorySeparatorChar)))));
    }

    private static IEnumerable<string> Elements(string root, string project, params string[] names) =>
        XDocument.Load(Path.Combine(root, project))
            .Descendants()
            .Where(element => names.Contains(element.Name.LocalName))
            .Select(element => element.Value.Trim())
            .Where(value => value.Length > 0);

    /// <summary>
    ///     A <c>ProjectReference</c> may differ in case from the folder on disk; the path filters must use the disk casing.
    /// </summary>
    private static string OnDisk(string root, string full)
    {
        var current = Path.GetFullPath(root);
        foreach (var part in Path.GetRelativePath(current, full).Split(Path.DirectorySeparatorChar))
        {
            var entry = Directory.Exists(current)
                ? Directory.EnumerateFileSystemEntries(current).FirstOrDefault(item => string.Equals(Path.GetFileName(item), part, StringComparison.OrdinalIgnoreCase))
                : null;
            current = entry ?? Path.Combine(current, part);
        }

        return Normalize(Path.GetRelativePath(root, current));
    }

    private static string Normalize(string path) => path.Replace('\\', '/');
}
