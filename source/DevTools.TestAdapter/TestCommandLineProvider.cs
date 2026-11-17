using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace DevTools.TestAdapter;

internal sealed class TestCommandLineProvider : ICommandLineOptionsProvider
{
    internal const string FilterOptionName = "filter";
    internal const string PidOptionName = "pid";

    public string Uid => "DevTools.TestAdapter.CommandLine";

    public string Version => "1.0.0";

    public string DisplayName => "DevTools.TestAdapter";

    public string Description => "Selects tests by method name and the host process that runs them.";

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions() =>
    [
        new(
            FilterOptionName,
            "Test method name (for example Arithmetic_runs_inside_host).",
            ArgumentArity.ExactlyOne,
            isHidden: false),
        new(
            PidOptionName,
            "Process id of the running host to run in (for example 12345). Without it the oldest matching host is reused.",
            ArgumentArity.ExactlyOne,
            isHidden: false),
    ];

    public Task<ValidationResult> ValidateOptionArgumentsAsync(
        CommandLineOption commandOption,
        string[] arguments)
    {
        if (commandOption.Name == FilterOptionName
            && (arguments.Length != 1 || string.IsNullOrWhiteSpace(arguments[0])))
        {
            return ValidationResult.InvalidTask("The --filter option requires a test method name.");
        }

        if (commandOption.Name == PidOptionName
            && (arguments.Length != 1 || !int.TryParse(arguments[0], out var pid) || pid <= 0))
        {
            return ValidationResult.InvalidTask("The --pid option requires a positive host process id.");
        }

        return ValidationResult.ValidTask;
    }

    public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions) =>
        ValidationResult.ValidTask;
}
