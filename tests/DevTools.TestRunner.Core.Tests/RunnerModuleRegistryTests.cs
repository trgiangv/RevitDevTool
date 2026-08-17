using DevTools.TestRunner.Core.Composition;
using DevTools.TestRunner.Core.Parsing;
using Microsoft.Extensions.DependencyInjection;

namespace DevTools.TestRunner.Core.Tests;

public sealed class RunnerModuleRegistryTests
{
    [Fact]
    public async Task RunAsync_normalizes_framework_and_invokes_selected_module()
    {
        var module = new RecordingModule("Example");
        var registry = new RunnerModuleRegistry();
        registry.Register(module, isDefault: true);

        var exitCode = await registry.RunAsync(["discover", "tests.dll", "--framework", " example "], new ServiceCollection().BuildServiceProvider());

        Assert.Equal(17, exitCode);
        Assert.Equal(["discover", "tests.dll", "--framework", " example "], module.Arguments);
        Assert.Equal(["example"], registry.RegisteredFrameworkIds);
    }

    [Fact]
    public void Register_rejects_empty_trimmed_and_duplicate_ids()
    {
        var registry = new RunnerModuleRegistry();

        Assert.Throws<ArgumentException>(() => registry.Register(new RecordingModule(" ")));
        registry.Register(new RecordingModule("Example"));
        Assert.Throws<InvalidOperationException>(() => registry.Register(new RecordingModule(" example ")));
    }

    [Fact]
    public void Command_context_owns_generic_host_debug_and_framework_parsing()
    {
        var registry = new RunnerModuleRegistry();
        registry.Register(new RecordingModule("Example"), isDefault: true);

        var created = RunnerCommandContext.TryCreate(
            registry, "run", @"C:\tests\Sample.dll", " Revit ", " 2026 ", true, 60, 180, false, 42, " EXAMPLE ", out var context, out var error);

        Assert.True(created, error);
        Assert.Equal("example", context!.FrameworkId);
        Assert.Equal("Revit", context.Host);
        Assert.Equal("2026", context.Version);
        Assert.True(context.Debug);
        Assert.True(context.UseGenericProtocol);
    }

    [Fact]
    public async Task RunAsync_selects_a_second_registered_provider_without_changing_dispatch()
    {
        var first = new RecordingModule("first");
        var second = new RecordingModule("second");
        var registry = new RunnerModuleRegistry();
        registry.Register(first, isDefault: true);
        registry.Register(second);

        var exitCode = await registry.RunAsync(["discover", "tests.dll", "--framework=SECOND"], new ServiceCollection().BuildServiceProvider());

        Assert.Equal(17, exitCode);
        Assert.Empty(first.Arguments);
        Assert.NotEmpty(second.Arguments);
    }

    [Fact]
    public async Task RunAsync_rejects_a_whitespace_framework_without_invoking_a_module()
    {
        var module = new RecordingModule("example");
        var registry = new RunnerModuleRegistry();
        registry.Register(module, isDefault: true);

        var exitCode = await registry.RunAsync(["discover", "tests.dll", "--framework", " "], new ServiceCollection().BuildServiceProvider());

        Assert.Equal(RunnerExitCode.CliError, exitCode);
        Assert.Empty(module.Arguments);
    }

    private sealed class RecordingModule(string frameworkId) : IRunnerCommandModule
    {
        public string FrameworkId => frameworkId;
        public IReadOnlyList<string> Arguments { get; private set; } = [];

        public void RegisterServices(IServiceCollection services) { }

        public Task<int> RunAsync(string[] args, IServiceProvider services)
        {
            Arguments = args;
            return Task.FromResult(17);
        }
    }
}
