using Build.Modules;
using Build.Options;
using Build.Trains;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ModularPipelines;
using ModularPipelines.Extensions;
using Sourcy.DotNet;
using System.Diagnostics.CodeAnalysis;

namespace Build;

public sealed record BuildCommand(string Target, Train? Train, bool Check, bool DryRun, string? Base, string? Version = null)
{
    public const string Usage = """
        Usage: dotnet run --project build -- <target> [train] [options]

        Targets:
          paths [--check]               write or check .github/train-paths.yml
          ci <train> [--base <sha>]     compile and test the train, then print the bump warnings
          pack <train>                  artifacts at a 0.0.0 version into output/<train>
          release <train> [--dry-run]   validate the release; without --dry-run also test and build it
          changelog <train>             rewrite the Unreleased block of the train changelog with git-cliff
          publish <daemon|addin|nettest>
                                        sign and push the built release of the train and create its GitHub Release
          channels <daemon|addin> --version <v>
                                        move the train in channels.json, sign it, upload it to the channels release

        Trains: daemon, addin, nettest, pytest
        """;

    public static BuildCommand? Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0) return null;

        var target = args[0];
        Train? train = null;
        bool check = false, dryRun = false;
        string? baseSha = null, version = null;
        for (var index = 1; index < args.Count; index++)
        {
            switch (args[index])
            {
                case "--check" when target == "paths":
                    check = true;
                    break;
                case "--dry-run" when target == "release":
                    dryRun = true;
                    break;
                case "--base" when target == "ci" && index + 1 < args.Count:
                    baseSha = args[++index];
                    break;
                case "--version" when target == "channels" && index + 1 < args.Count:
                    version = args[++index];
                    break;
                case var name when train is null && Train.Find(name) is { } found:
                    train = found;
                    break;
                default:
                    return null;
            }
        }

        return target switch
        {
            "paths" when train is null => new BuildCommand(target, null, check, false, null),
            "ci" or "pack" or "release" or "publish" or "changelog" when train is not null => new BuildCommand(target, train, false, dryRun, baseSha),
            "channels" when train is not null && version is not null => new BuildCommand(target, train, false, false, null, version),
            _ => null
        };
    }
}

[SuppressMessage("Usage", "ConsoleUse", Justification = "The command line runs before and after the pipeline.")]
public static class Cli
{
    public static async Task<int> RunAsync(string[] args)
    {
        var command = BuildCommand.Parse(args);
        if (command is null)
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
                "ci" => await CiAsync(root, command),
                "pack" => await PackAsync(root, command.Train!),
                "release" => await ReleaseAsync(root, command),
                "publish" => Publish(root, command.Train!),
                "changelog" => Changelog(root, command.Train!),
                "channels" => Done(await Channels.RunAsync(root, command.Train!, command.Version!)),
                _ => Fail($"Target '{command.Target}' is not available yet.")
            };
        }
        catch (InvalidOperationException exception)
        {
            return Fail(exception.Message);
        }
    }

    private static int Changelog(string root, Train train)
    {
        ChangelogNotes.Update(root, train);
        Console.WriteLine($"Updated the Unreleased block of {train.Changelog}.");
        return 0;
    }

    private static int Paths(string root, bool check)
    {
        if (!check)
        {
            TrainPaths.Write(root);
            Console.WriteLine($"Wrote {TrainPaths.FilterFile}.");
            return 0;
        }

        if (TrainPaths.IsCurrent(root))
        {
            Console.WriteLine($"{TrainPaths.FilterFile} is current.");
            return 0;
        }

        return Fail($"{TrainPaths.FilterFile} is stale; run 'dotnet run --project build -- paths' and commit it.");
    }

    private static async Task<int> CiAsync(string root, BuildCommand command)
    {
        var train = command.Train!;
        if (Paths(root, check: true) != 0) return 1;

        foreach (var warning in BumpWarnings.Collect(root, train, command.Base))
        {
            Console.WriteLine($"::warning::{warning}");
        }

        var version = new TrainVersion(train, VersionFile.Read(root, train.VersionFile), false, null);
        await RunPipelineAsync(new BuildRun(root, train, BuildTarget.Ci, version), services => AddCi(services, train));
        return 0;
    }

    private static async Task<int> PackAsync(string root, Train train)
    {
        var number = TrainVersion.NonRelease(train, Environment.GetEnvironmentVariable("GITHUB_RUN_NUMBER"), DateTimeOffset.UtcNow);
        var version = new TrainVersion(train, number, true, TrainVersion.PickPrevious(train, Git.Tags(root, train)));
        Console.WriteLine($"Packing {train.Name} {number}.");
        await RunPipelineAsync(new BuildRun(root, train, BuildTarget.Pack, version), services => AddArtifacts(services, train, release: false));
        return 0;
    }

    private static async Task<int> ReleaseAsync(string root, BuildCommand command)
    {
        var train = command.Train!;
        var plan = ReleasePlan.Create(root, train);
        Console.Write(plan.Render());
        foreach (var warning in plan.Warnings)
        {
            Console.WriteLine($"::warning::{warning}");
        }

        if (plan.Refusals.Count > 0)
        {
            foreach (var refusal in plan.Refusals) Console.Error.WriteLine(refusal);
            return 1;
        }

        plan.WriteGitHubFiles();
        if (command.DryRun) return 0;

        await RunPipelineAsync(new BuildRun(root, train, BuildTarget.Release, plan.Version), services =>
        {
            AddCi(services, train);
            AddArtifacts(services, train, release: true);
        });
        var outputDir = Directory.CreateDirectory(Path.Combine(root, "output", train.Name)).FullName;
        if (train == Train.Daemon || train == Train.Addin) ReleaseAssets.Verify(plan.Version, outputDir, signed: false);
        File.WriteAllText(Path.Combine(outputDir, "notes.md"), ChangelogNotes.ForRelease(root, train, plan.Version.Version) + "\n");
        return 0;
    }

    private static int Publish(string root, Train train)
    {
        if (train == Train.PyTest) return Fail("The pytest train publishes to PyPI from release.yml; there is no publish target for it.");

        Console.WriteLine(train == Train.NetTest ? NuGetPublish.Run(root, train) : GitHubPublish.Run(root, train));
        return 0;
    }

    private static void AddCi(IServiceCollection services, Train train)
    {
        if (train == Train.PyTest)
        {
            services.AddModule<TestPyTestModule>();
            return;
        }

        AddCompile(services, train);
        services.AddModule<TestTrainModule>();
    }

    private static void AddArtifacts(IServiceCollection services, Train train, bool release)
    {
        if (train == Train.PyTest)
        {
            services.AddModule<PackPyTestModule>();
            return;
        }

        if (!release && train != Train.NetTest) AddCompile(services, train);

        if (train == Train.Daemon)
        {
            services.AddModule<PackDaemonModule>();
        }
        else if (train == Train.Addin)
        {
            services.AddModule<CreateBundleModule>();
            services.AddModule<PackPayloadsModule>();
            services.AddModule<GenerateUpdateManifestModule>();
        }
        else if (train == Train.NetTest)
        {
            services.AddModule<PackTestAdapterModule>();
        }
    }

    private static void AddCompile(IServiceCollection services, Train train)
    {
        if (train == Train.Addin)
        {
            services.AddModule<ResolveConfigsModule>();
            services.AddModule<CompileBaseModule>();
            services.AddModule<CompileExpandModule>();
        }
        else
        {
            services.AddModule<CompileTrainModule>();
        }
    }

    private static async Task RunPipelineAsync(BuildRun run, Action<IServiceCollection> addModules)
    {
        var builder = Pipeline.CreateBuilder();
        builder.Configuration.AddJsonFile("appsettings.json");
        builder.Configuration.AddUserSecrets<BuildRun>();
        builder.Configuration.AddEnvironmentVariables();

        builder.Services.AddOptions<BundleOptions>().Bind(builder.Configuration.GetSection("Bundle"));
        builder.Services.AddOptions<UpdateOptions>().Bind(builder.Configuration.GetSection("Update"));
        builder.Services.AddSingleton(run);
        addModules(builder.Services);

        await builder.Build().RunAsync();
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
