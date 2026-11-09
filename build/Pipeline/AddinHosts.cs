using System.Security.Cryptography;

namespace Build.Pipeline;

/// <summary>
///     The add-in compile leaves one folder per host version in <c>output/hosts</c>: the bundle's <c>Contents\&lt;host version&gt;</c>
///     of both hosts, and <c>&lt;host version&gt;.version</c> with the version stamped into it. <c>pack addin</c> bundles only these
///     folders, so each host version can compile on its own runner.
/// </summary>
public static class AddinHosts
{
    private const string VersionExtension = ".version";

    public static string Folder(string root) => Path.Combine(root, "output", "hosts");

    public static string HostVersion(string configuration) => configuration[(configuration.LastIndexOf('.') + 1)..];

    /// <summary>
    ///     Merges the publish folders of both hosts. Hosts of one host version share the folder, so a file both publish must be
    ///     byte-identical (0046).
    /// </summary>
    public static void Collect(BuildRun run, string configuration)
    {
        var hostVersion = HostVersion(configuration);
        var target = Path.Combine(Folder(run.Root), hostVersion);
        if (Directory.Exists(target)) Directory.Delete(target, recursive: true);

        foreach (var project in run.Product.Roots)
        {
            var source = Path.Combine(run.Root, Path.GetDirectoryName(project)!, "bin", configuration, "publish", "Contents", hostVersion);
            if (!Directory.Exists(source))
                throw new InvalidOperationException($"{Path.GetFileNameWithoutExtension(project)} published nothing into {source}.");

            Merge(source, target, hostVersion);
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

    private static void Merge(string source, string target, string hostVersion)
    {
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(source, file);
            var destination = Path.Combine(target, relativePath);
            if (File.Exists(destination))
            {
                if (!HaveSameContent(file, destination))
                    throw new InvalidOperationException($"Contents\\{hostVersion}\\{relativePath} differs between hosts of the same host version: '{file}' vs '{destination}'.");
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination);
        }
    }

    private static bool HaveSameContent(string left, string right)
    {
        if (new FileInfo(left).Length != new FileInfo(right).Length) return false;

        using var leftStream = File.OpenRead(left);
        using var rightStream = File.OpenRead(right);
        return SHA256.HashData(leftStream).AsSpan().SequenceEqual(SHA256.HashData(rightStream));
    }
}
