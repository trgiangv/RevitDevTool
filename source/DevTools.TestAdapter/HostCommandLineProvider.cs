using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.CommandLine;

namespace DevTools.TestAdapter;

internal sealed class HostCommandLineProvider : ICommandLineOptionsProvider
{
    internal const string FilterOptionName = "filter";

    public string Uid => "DevTools.TestAdapter.CommandLine";

    public string Version => "1.0.0";

    public string DisplayName => "DevTools.TestAdapter";

    public string Description => "Selects host tests by method name.";

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public IReadOnlyCollection<CommandLineOption> GetCommandLineOptions() =>
    [
        new(
            FilterOptionName,
            "Test method name (for example Arithmetic_runs_inside_host).",
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

        return ValidationResult.ValidTask;
    }

    public Task<ValidationResult> ValidateCommandLineOptionsAsync(ICommandLineOptions commandLineOptions) =>
        ValidationResult.ValidTask;
}
