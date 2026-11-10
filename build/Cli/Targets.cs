using Build.Modules;
using Build.Pipeline;
using Build.Products;
using Build.Releases;
using Build.Tools;
using ConsoleAppFramework;
using Sourcy.DotNet;
using System.Diagnostics.CodeAnalysis;

namespace Build.Cli;

/// <summary>
///     One command per target. A refusal is one sentence on stderr and exit code 1.
/// </summary>
[SuppressMessage("Usage", "ConsoleUse", Justification = "The command line runs before and after the pipeline.")]
public sealed class Targets
{
    private static readonly string Root = Path.GetDirectoryName(Solutions.RevitDevTool.FullName)!;
    private static readonly string StalePaths = $"{ProductPaths.FilterFile} or {CiJobs.JobsFile} is stale; run 'dotnet run --project build -- paths' and commit them.";

    /// <summary>Write or check .github/product-paths.yml and .github/ci-jobs.json.</summary>
    /// <param name="check">Fail when a file is stale instead of writing it.</param>
    [Command("paths")]
    public async Task<int> Paths(bool check = false)
    {
        if (!check)
        {
            ProductPaths.Write(Root);
            CiJobs.Write(Root, await ResolveConfigsModule.HostVersionsAsync(CancellationToken.None));
            return Done($"Wrote {ProductPaths.FilterFile} and {CiJobs.JobsFile}.");
        }

        return await PathsAreCurrentAsync() ? Done($"{ProductPaths.FilterFile} and {CiJobs.JobsFile} are current.") : Fail(StalePaths);
    }

    /// <summary>Compile; the add-in also collects each host version into output/hosts.</summary>
    /// <param name="product">daemon, addin, nettest or pytest.</param>
    /// <param name="hostVersion">Compile the add-in for this host version only.</param>
    [Command("compile")]
    public async Task<int> Compile([Argument] Product product, string? hostVersion = null)
    {
        if (hostVersion is null) return await RunStagesAsync(product, Stage.Compile);
        if (product != Product.Addin) return Fail("--host-version applies to the add-in only.");

        return (await ResolveConfigsModule.HostVersionsAsync(CancellationToken.None)).Contains(hostVersion)
            ? await RunStagesAsync(product, Stage.Compile, hostVersion: hostVersion)
            : Fail($"Unknown host version '{hostVersion}'; use one of the Release.Autodesk.* configurations in RevitDevTool.slnx.");
    }

    /// <summary>Check the path filters, print the bump warnings, run the tests.</summary>
    /// <param name="product">daemon, addin, nettest or pytest.</param>
    /// <param name="base">The commit the bump warnings compare against; in CI the base of the GitHub event.</param>
    [Command("test")]
    public Task<int> Test([Argument] Product product, string? @base = null) => RunStagesAsync(product, Stage.Test, @base);

    /// <summary>Compile, then test.</summary>
    /// <param name="product">daemon, addin, nettest or pytest.</param>
    /// <param name="base">The commit the bump warnings compare against; in CI the base of the GitHub event.</param>
    [Command("ci")]
    public Task<int> Ci([Argument] Product product, string? @base = null) => RunStagesAsync(product, Stage.Compile | Stage.Test, @base);

    /// <summary>Artifacts at a 0.0.0 version into output/&lt;product&gt;; pack addin bundles a prior compile.</summary>
    /// <param name="product">daemon, addin, nettest or pytest.</param>
    [Command("pack")]
    public Task<int> Pack([Argument] Product product) => RunStagesAsync(product, Stage.Pack);

    /// <summary>Validate the release; without --dry-run also compile, test and pack it, and write notes.md.</summary>
    /// <param name="product">daemon, addin, nettest or pytest.</param>
    /// <param name="dryRun">Only validate.</param>
    [Command("release")]
    public async Task<int> Release([Argument] Product product, bool dryRun = false)
    {
        var plan = ReleasePlan.Create(Root, product);
        Console.Write(plan.Render());
        Warn(plan.Warnings);
        if (plan.Refusals.Count > 0) return Fail(string.Join(Environment.NewLine, plan.Refusals));

        plan.WriteGitHubFiles();
        if (dryRun) return 0;

        var run = new BuildRun(Root, product, Stage.Release, plan.Version);
        await ProductPipeline.RunAsync(run);
        if (product == Product.Daemon || product == Product.Addin) ReleaseAssets.Verify(plan.Version, run.Output, signed: false);
        await File.WriteAllTextAsync(Path.Combine(run.Output, "notes.md"), ChangelogNotes.ForRelease(Root, product, plan.Version.Version) + "\n");
        return 0;
    }

    /// <summary>Rewrite the Unreleased block of the product changelog with git-cliff.</summary>
    /// <param name="product">daemon, addin, nettest or pytest.</param>
    [Command("changelog")]
    public int Changelog([Argument] Product product)
    {
        ChangelogNotes.Update(Root, product);
        return Done($"Updated the Unreleased block of {product.Changelog}.");
    }

    /// <summary>Sign and push the built release and create its GitHub Release.</summary>
    /// <param name="product">daemon, addin or nettest.</param>
    [Command("publish")]
    public int Publish([Argument] Product product) => Done(Releases.Publish.Run(Root, product));

    /// <summary>Move the product in channels.json, sign it, upload it to the channels release.</summary>
    /// <param name="product">daemon or addin.</param>
    /// <param name="version">The released version to move.</param>
    [Command("channels")]
    public async Task<int> Channels([Argument] Product product, string version) => Done(await Releases.Channels.RunAsync(Root, product, version));

    /// <summary>
    ///     Runs the stages at a <c>0.0.0</c> version; <c>pack addin</c> keeps the version its host versions were compiled at.
    ///     The test stage first checks the path filters and prints the bump warnings.
    /// </summary>
    private static async Task<int> RunStagesAsync(Product product, Stage stages, string? @base = null, string? hostVersion = null)
    {
        if (stages.HasFlag(Stage.Test))
        {
            if (!await PathsAreCurrentAsync()) return Fail(StalePaths);
            Warn(BumpWarnings.Collect(Root, product, @base ?? GitHubEvent.BaseSha()));
        }

        var number = product == Product.Addin && stages == Stage.Pack
            ? await CompiledAddinVersionAsync()
            : ProductVersion.NonRelease(product, Environment.GetEnvironmentVariable("GITHUB_RUN_NUMBER"), DateTimeOffset.UtcNow);
        var version = new ProductVersion(product, number, true, ProductVersion.PickPrevious(product, Git.Tags(Root, product)));
        Console.WriteLine($"{product.Name} {stages} {number} {hostVersion}".TrimEnd());
        await ProductPipeline.RunAsync(new BuildRun(Root, product, stages, version, hostVersion));
        return 0;
    }

    private static async Task<bool> PathsAreCurrentAsync() =>
        ProductPaths.IsCurrent(Root) && CiJobs.IsCurrent(Root, await ResolveConfigsModule.HostVersionsAsync(CancellationToken.None));

    private static async Task<string> CompiledAddinVersionAsync()
    {
        var version = AddinHosts.CompiledVersion(Root);
        AddinHosts.Require(Root, await ResolveConfigsModule.HostVersionsAsync(CancellationToken.None), version);
        return version;
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
