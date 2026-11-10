using System.Security.Cryptography;
using System.Xml.Linq;

namespace Build.Pipeline;

/// <summary>
///     The add-in compile leaves one folder per host version in <c>output/hosts</c>: <c>shared</c> holds the files that every host
///     publishes, <c>&lt;bundle&gt;</c> the files of one host, and <c>&lt;host version&gt;.version</c> the version stamped into them.
///     <c>pack addin</c> bundles only these folders, so each host version can compile on its own runner.
/// </summary>
public static class AddinHosts
{
    public const string Shared = "shared";
    private const string VersionExtension = ".version";

    public static string Folder(string root) => Path.Combine(root, "output", "hosts");

    public static string HostVersion(string configuration) => configuration[(configuration.LastIndexOf('.') + 1)..];

    /// <summary>
    ///     Splits the publish folders of the hosts into <see cref="Shared"/> and one folder per bundle. A file that two hosts
    ///     publish must be byte-identical (0046).
    /// </summary>
    public static void Collect(BuildRun run, string configuration)
    {
        var hostVersion = HostVersion(configuration);
        var target = Path.Combine(Folder(run.Root), hostVersion);
        if (Directory.Exists(target)) Directory.Delete(target, recursive: true);

        var hosts = run.Product.Roots.Select(project => Publish(run.Root, project, configuration, hostVersion)).ToArray();
        var files = hosts.SelectMany(host => Directory.EnumerateFiles(host.Folder, "*", SearchOption.AllDirectories)
                .Select(file => (host.Bundle, File: file, Relative: Path.GetRelativePath(host.Folder, file))))
            .GroupBy(item => item.Relative, StringComparer.OrdinalIgnoreCase);

        foreach (var group in files)
        {
            var copies = group.ToArray();
            if (copies.Skip(1).FirstOrDefault(copy => !HaveSameContent(copies[0].File, copy.File)) is { File: not null } differs)
                throw new InvalidOperationException($"Contents\\{hostVersion}\\{group.Key} differs between hosts of the same host version: '{copies[0].File}' vs '{differs.File}'.");

            var folders = copies.Length == hosts.Length ? [Shared] : copies.Select(copy => copy.Bundle);
            foreach (var folder in folders) Copy(copies[0].File, Path.Combine(target, folder, group.Key));
        }

        File.WriteAllText(target + VersionExtension, run.Version.Version);
    }

    /// <summary>
    ///     The version the host versions in <c>output/hosts</c> were compiled at.
    /// </summary>
    public static string CompiledVersion(string root)
    {
        var folder = Folder(root);
        var versions = Directory.Exists(folder)
            ? Directory.GetFiles(folder, "*" + VersionExtension).Select(File.ReadAllText).Distinct(StringComparer.Ordinal).ToArray()
            : [];
        return versions.Length switch
        {
            0 => throw new InvalidOperationException($"{folder} holds no compiled host version; run 'compile addin' first."),
            1 => versions[0],
            _ => throw new InvalidOperationException($"{folder} holds host versions of different compiles ({string.Join(", ", versions)}); run 'compile addin' again.")
        };
    }

    /// <summary>
    ///     The folder of each host version, after checking that every one is there and compiled at <paramref name="version"/>.
    /// </summary>
    public static string[] Require(string root, IEnumerable<string> hostVersions, string version)
    {
        var folder = Folder(root);
        var wrong = hostVersions
            .Where(hostVersion => !File.Exists(Path.Combine(folder, hostVersion + VersionExtension)) ||
                                  File.ReadAllText(Path.Combine(folder, hostVersion + VersionExtension)) != version)
            .ToArray();
        if (wrong.Length > 0)
            throw new InvalidOperationException($"{folder} lacks host version {string.Join(", ", wrong)} compiled at {version}; run 'compile addin' first.");

        return hostVersions.Select(hostVersion => Path.Combine(folder, hostVersion)).ToArray();
    }

    /// <summary>
    ///     The bundle comes from the <c>Name</c> in the <c>PackageContents.xml</c> that the project publishes next to <c>Contents</c>.
    /// </summary>
    private static (string Bundle, string Folder) Publish(string root, string project, string configuration, string hostVersion)
    {
        var publish = Path.Combine(root, Path.GetDirectoryName(project)!, "bin", configuration, "publish");
        var contents = Path.Combine(publish, "Contents", hostVersion);
        var manifest = Path.Combine(publish, "PackageContents.xml");
        if (!Directory.Exists(contents) || !File.Exists(manifest))
            throw new InvalidOperationException($"{Path.GetFileNameWithoutExtension(project)} published nothing into {publish}.");

        return (XDocument.Load(manifest).Root!.Attribute("Name")!.Value + ".bundle", contents);
    }

    private static void Copy(string source, string destination)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        File.Copy(source, destination);
    }

    private static bool HaveSameContent(string left, string right)
    {
        if (new FileInfo(left).Length != new FileInfo(right).Length) return false;

        using var leftStream = File.OpenRead(left);
        using var rightStream = File.OpenRead(right);
        return SHA256.HashData(leftStream).AsSpan().SequenceEqual(SHA256.HashData(rightStream));
    }
}
