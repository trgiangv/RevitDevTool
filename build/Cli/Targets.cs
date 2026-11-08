using Build.Pipeline;
using Build.Releases;
using Build.Tools;
using Build.Products;
using Sourcy.DotNet;
using System.Diagnostics.CodeAnalysis;

namespace Build.Cli;

/// <summary>
///     One method per target. A refusal is one sentence on stderr and exit code 1.
/// </summary>
[SuppressMessage("Usage", "ConsoleUse", Justification = "The command line runs before and after the pipeline.")]
public static class Targets
{
    public static async Task<int> RunAsync(string[] args)
    {
        if (BuildCommand.Parse(args) is not { } command)
        {
            Console.Error.WriteLine(BuildCommand.Usage);
            return 1;
        }

        var root = Path.GetDirectoryName(Solutions.RevitDevTool.FullName)!;
        try
        {
            return command.Target switch
            {
                "paths" => Paths(root, command.Check),
                "ci" => await CiAsync(root, command.Product!, command.Base),
                "pack" => await PackAsync(root, command.Product!),
                "release" => await ReleaseAsync(root, command.Product!, command.DryRun),
                "changelog" => Changelog(root, command.Product!),
                "publish" => Done(Publish.Run(root, command.Product!)),
                _ => Done(await Channels.RunAsync(root, command.Product!, command.Version!))
            };
        }
        catch (InvalidOperationException exception)
        {
            return Fail(exception.Message);
        }
    }

    private static int Paths(string root, bool check)
    {
        if (!check)
        {
            ProductPaths.Write(root);
            return Done($"Wrote {ProductPaths.FilterFile}.");
        }

        return ProductPaths.IsCurrent(root)
            ? Done($"{ProductPaths.FilterFile} is current.")
            : Fail($"{ProductPaths.FilterFile} is stale; run 'dotnet run --project build -- paths' and commit it.");
    }

    private static async Task<int> CiAsync(string root, Product product, string? baseSha)
    {
        if (Paths(root, check: true) != 0) return 1;

        Warn(BumpWarnings.Collect(root, product, baseSha));
        var version = new ProductVersion(product, VersionFile.Read(root, product.VersionFile), false, null);
        await ProductPipeline.RunAsync(new BuildRun(root, product, BuildTarget.Ci, version));
        return 0;
    }

    private static async Task<int> PackAsync(string root, Product product)
    {
        var number = ProductVersion.NonRelease(product, Environment.GetEnvironmentVariable("GITHUB_RUN_NUMBER"), DateTimeOffset.UtcNow);
        var version = new ProductVersion(product, number, true, ProductVersion.PickPrevious(product, Git.Tags(root, product)));
        Console.WriteLine($"Packing {product.Name} {number}.");
        await ProductPipeline.RunAsync(new BuildRun(root, product, BuildTarget.Pack, version));
        return 0;
    }

    /// <summary>
    ///     Validates, then tests and builds the artifacts at the file version and writes <c>notes.md</c> for the publish job.
    /// </summary>
    private static async Task<int> ReleaseAsync(string root, Product product, bool dryRun)
    {
        var plan = ReleasePlan.Create(root, product);
        Console.Write(plan.Render());
        Warn(plan.Warnings);
        if (plan.Refusals.Count > 0) return Fail(string.Join(Environment.NewLine, plan.Refusals));

        plan.WriteGitHubFiles();
        if (dryRun) return 0;

        var run = new BuildRun(root, product, BuildTarget.Release, plan.Version);
        await ProductPipeline.RunAsync(run);
        if (product == Product.Daemon || product == Product.Addin) ReleaseAssets.Verify(plan.Version, run.Output, signed: false);
        await File.WriteAllTextAsync(Path.Combine(run.Output, "notes.md"), ChangelogNotes.ForRelease(root, product, plan.Version.Version) + "\n");
        return 0;
    }

    private static int Changelog(string root, Product product)
    {
        ChangelogNotes.Update(root, product);
        return Done($"Updated the Unreleased block of {product.Changelog}.");
    }

    private static void Warn(IEnumerable<string> warnings)
    {
        foreach (var warning in warnings) Console.WriteLine($"::warning::{warning}");
    }

    private static int Done(string message)
    {
        Console.WriteLine(message);
        return 0;
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }
}
