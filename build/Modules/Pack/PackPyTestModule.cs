using Build.Pipeline;
using Build.Tools;
using Build.Products;
using ModularPipelines.Attributes;
using ModularPipelines.Context;
using ModularPipelines.Modules;

namespace Build.Modules;

/// <summary>
///     Builds the wheel and sdist into <c>output/pytest</c> from a temp copy of the package whose
///     <c>pyproject.toml</c> carries the product version, so the working tree keeps its version line.
/// </summary>
[DependsOn<TestPyTestModule>(Optional = true)]
[UsedImplicitly]
public sealed class PackPyTestModule(BuildRun run) : Module<string[]>
{
    private static readonly string[] PackageFiles = ["pyproject.toml", "README.md", "LICENSE"];

    protected override async Task<string[]?> ExecuteAsync(IModuleContext context, CancellationToken cancellationToken)
    {
        var version = run.Version.Version;
        var source = Path.Combine(run.Root, ModuleShell.PyTestProject);
        var outputDir = Directory.CreateDirectory(run.Output).FullName;
        foreach (var stale in Directory.GetFiles(outputDir)) File.Delete(stale);

        var staging = Directory.CreateTempSubdirectory("pytest-pack-").FullName;
        try
        {
            foreach (var file in PackageFiles) File.Copy(Path.Combine(source, file), Path.Combine(staging, file));
            CopyFolder(Path.Combine(source, "src"), Path.Combine(staging, "src"));
            var pyproject = Path.Combine(staging, "pyproject.toml");
            await File.WriteAllTextAsync(pyproject, VersionFile.WithTomlVersion(await File.ReadAllTextAsync(pyproject, cancellationToken), version), cancellationToken);

            await ModuleShell.PixiAsync(context, run.Root, ["build", "--outdir", outputDir, staging], cancellationToken);
        }
        finally
        {
            Directory.Delete(staging, recursive: true);
        }

        var artifacts = run.Product.Artifacts.Select(artifact => Path.Combine(outputDir, string.Format(artifact, version))).ToArray();
        var missing = artifacts.Where(artifact => !File.Exists(artifact)).ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException($"Expected {string.Join(", ", missing)}.");
        }

        context.Summary.KeyValue("Artifacts", "PyTest", string.Join(", ", artifacts));
        return artifacts;
    }

    private static void CopyFolder(string from, string to)
    {
        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(from, file);
            if (relative.Split(Path.DirectorySeparatorChar).Contains("__pycache__")) continue;

            var target = Path.Combine(to, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
    }
}
