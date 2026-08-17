using DevTools.TestRunner.Core.Parsing;
using Microsoft.Extensions.DependencyInjection;

namespace DevTools.TestRunner.Core.Composition;

/// <summary>Registers explicitly supplied runner modules without discovery or reflection.</summary>
public sealed class RunnerModuleRegistry
{
    private readonly Dictionary<string, IRunnerCommandModule> modules = new(StringComparer.Ordinal);
    private string? defaultFrameworkId;

    public IReadOnlyCollection<string> RegisteredFrameworkIds => modules.Keys;

    public void Register(IRunnerCommandModule module, bool isDefault = false)
    {
        ArgumentNullException.ThrowIfNull(module);
        var frameworkId = NormalizeFrameworkId(module.FrameworkId);
        if (isDefault && defaultFrameworkId is not null)
            throw new InvalidOperationException("A default runner module is already registered.");
        if (!modules.TryAdd(frameworkId, module))
            throw new InvalidOperationException($"Runner module '{frameworkId}' is already registered.");

        if (!isDefault)
            return;
        defaultFrameworkId = frameworkId;
    }

    public void RegisterServices(IServiceCollection services)
    {
        foreach (var module in modules.Values)
            module.RegisterServices(services);
    }

    public async Task<int> RunAsync(string[] args, IServiceProvider services)
    {
        if (!TrySelect(args, out var module, out var error))
        {
            await Console.Error.WriteLineAsync(error ?? "Invalid command line.").ConfigureAwait(false);
            return RunnerExitCode.CliError;
        }

        return await module!.RunAsync(args, services).ConfigureAwait(false);
    }

    public bool TrySelect(string[] args, out IRunnerCommandModule? module, out string? error)
    {
        module = null;
        error = null;
        var requestedFramework = ReadFrameworkArgument(args, out error);
        if (error is not null)
            return false;

        string? frameworkId;
        try
        {
            frameworkId = requestedFramework is null ? defaultFrameworkId : NormalizeFrameworkId(requestedFramework);
        }
        catch (ArgumentException exception)
        {
            error = exception.Message;
            return false;
        }
        if (frameworkId is null)
        {
            error = "--framework is required because no default runner module is registered.";
            return false;
        }

        if (!modules.TryGetValue(frameworkId, out module))
        {
            error = $"Unsupported --framework '{frameworkId}'.";
            return false;
        }

        return true;
    }

    public string GetDefaultFrameworkId() => defaultFrameworkId
        ?? throw new InvalidOperationException("No default runner module is registered.");

    public static string NormalizeFrameworkId(string frameworkId)
    {
        if (string.IsNullOrWhiteSpace(frameworkId))
            throw new ArgumentException("Framework ID is required.", nameof(frameworkId));
        return frameworkId.Trim().ToLowerInvariant();
    }

    private static string? ReadFrameworkArgument(IReadOnlyList<string> args, out string? error)
    {
        error = null;
        string? framework = null;
        for (var index = 0; index < args.Count; index++)
        {
            var argument = args[index];
            if (argument.StartsWith("--framework=", StringComparison.Ordinal))
            {
                framework = argument[12..];
                continue;
            }

            if (!string.Equals(argument, "--framework", StringComparison.Ordinal))
                continue;
            if (++index >= args.Count)
            {
                error = "--framework requires a framework id.";
                return null;
            }

            framework = args[index];
        }

        return framework;
    }

}
