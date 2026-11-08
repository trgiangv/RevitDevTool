using Build.Trains;

namespace Build.Cli;

/// <summary>
///     <c>dotnet run --project build -- &lt;target&gt; [train] [options]</c>. Each option belongs to one target.
/// </summary>
public sealed record BuildCommand(string Target, Train? Train, bool Check = false, bool DryRun = false, string? Base = null, string? Version = null)
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

        var command = new BuildCommand(args[0], null);
        for (var index = 1; index < args.Count; index++)
        {
            var value = index + 1 < args.Count ? args[index + 1] : null;
            switch (args[index], command.Target)
            {
                case ("--check", "paths"):
                    command = command with { Check = true };
                    break;
                case ("--dry-run", "release"):
                    command = command with { DryRun = true };
                    break;
                case ("--base", "ci") when value is not null:
                    command = command with { Base = value };
                    index++;
                    break;
                case ("--version", "channels") when value is not null:
                    command = command with { Version = value };
                    index++;
                    break;
                case var (name, _) when command.Train is null && Train.Find(name) is { } train:
                    command = command with { Train = train };
                    break;
                default:
                    return null;
            }
        }

        return command.IsComplete ? command : null;
    }

    private bool IsComplete => Target switch
    {
        "paths" => Train is null,
        "ci" or "pack" or "release" or "publish" or "changelog" => Train is not null,
        "channels" => Train is not null && Version is not null,
        _ => false
    };
}
