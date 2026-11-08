using Build.Pipeline;
using Build.Releases;
using Build.Tools;
using Build.Trains;
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
                "ci" => await CiAsync(root, command.Train!, command.Base),
                "pack" => await PackAsync(root, command.Train!),
                "release" => await ReleaseAsync(root, command.Train!, command.DryRun),
                "changelog" => Changelog(root, command.Train!),
                "publish" => Done(Publish.Run(root, command.Train!)),
                _ => Done(await Channels.RunAsync(root, command.Train!, command.Version!))
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
            TrainPaths.Write(root);
            return Done($"Wrote {TrainPaths.FilterFile}.");
        }

        return TrainPaths.IsCurrent(root)
            ? Done($"{TrainPaths.FilterFile} is current.")
            : Fail($"{TrainPaths.FilterFile} is stale; run 'dotnet run --project build -- paths' and commit it.");
    }

    private static async Task<int> CiAsync(string root, Train train, string? baseSha)
    {
        if (Paths(root, check: true) != 0) return 1;

        Warn(BumpWarnings.Collect(root, train, baseSha));
        var version = new TrainVersion(train, VersionFile.Read(root, train.VersionFile), false, null);
        await TrainPipeline.RunAsync(new BuildRun(root, train, BuildTarget.Ci, version));
        return 0;
    }

    private static async Task<int> PackAsync(string root, Train train)
    {
        var number = TrainVersion.NonRelease(train, Environment.GetEnvironmentVariable("GITHUB_RUN_NUMBER"), DateTimeOffset.UtcNow);
        var version = new TrainVersion(train, number, true, TrainVersion.PickPrevious(train, Git.Tags(root, train)));
        Console.WriteLine($"Packing {train.Name} {number}.");
        await TrainPipeline.RunAsync(new BuildRun(root, train, BuildTarget.Pack, version));
        return 0;
    }

    /// <summary>
    ///     Validates, then tests and builds the artifacts at the file version and writes <c>notes.md</c> for the publish job.
    /// </summary>
    private static async Task<int> ReleaseAsync(string root, Train train, bool dryRun)
    {
        var plan = ReleasePlan.Create(root, train);
        Console.Write(plan.Render());
        Warn(plan.Warnings);
        if (plan.Refusals.Count > 0) return Fail(string.Join(Environment.NewLine, plan.Refusals));

        plan.WriteGitHubFiles();
        if (dryRun) return 0;

        var run = new BuildRun(root, train, BuildTarget.Release, plan.Version);
        await TrainPipeline.RunAsync(run);
        if (train == Train.Daemon || train == Train.Addin) ReleaseAssets.Verify(plan.Version, run.Output, signed: false);
        await File.WriteAllTextAsync(Path.Combine(run.Output, "notes.md"), ChangelogNotes.ForRelease(root, train, plan.Version.Version) + "\n");
        return 0;
    }

    private static int Changelog(string root, Train train)
    {
        ChangelogNotes.Update(root, train);
        return Done($"Updated the Unreleased block of {train.Changelog}.");
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
