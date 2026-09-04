using System.Reflection;
using DevTools.Execution;
using DevTools.Execution.Abstractions;
using DevTools.Execution.Interfaces;
using DevTools.Execution.Models;
using DevTools.Execution.Providers.Python;
using DevTools.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Python.Runtime;

namespace DevTools.Execution.Tests;

internal static class ExecutionTestHelpers
{
    public static string CreateTempDirectory(string prefix = "execution-test")
    {
        var directory = Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    public static IHostContextExecutor InlineHostContext() => new InlineHostContextExecutor();

    public static Mock<IHostContextExecutor> MockHostContext()
    {
        var mock = new Mock<IHostContextExecutor>();
        mock.Setup(h => h.ExecuteAsync(It.IsAny<Func<ExecutionResult>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<ExecutionResult>, CancellationToken>((handler, _) => Task.FromResult(handler()));
        mock.Setup(h => h.ExecuteAsync(It.IsAny<Action>(), It.IsAny<CancellationToken>()))
            .Returns<Action, CancellationToken>((action, _) =>
            {
                action();
                return Task.CompletedTask;
            });
        return mock;
    }

    public static ICompiledScriptBridge CreateScriptBridge(string commandTypeName = "ScriptCommand")
    {
        return new InlineCompiledScriptBridge(commandTypeName);
    }

    public static ServiceProvider BuildExecutionServiceProvider(
        IHostContextExecutor? hostContext = null,
        ICompiledScriptBridge? scriptBridge = null,
        ICommandDiscovery? commandDiscovery = null,
        ICommandRunner? commandRunner = null,
        IIronPythonBridge? ironPythonBridge = null,
        IPythonBridge? pythonBridge = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostAppInfo>(new FakeHostAppInfo());
        services.AddSingleton(hostContext ?? InlineHostContext());
        services.AddSingleton(scriptBridge ?? CreateScriptBridge());
        services.AddSingleton(commandDiscovery ?? Mock.Of<ICommandDiscovery>());
        services.AddSingleton(commandRunner ?? Mock.Of<ICommandRunner>());
        services.AddSingleton(ironPythonBridge ?? Mock.Of<IIronPythonBridge>());
        services.AddSingleton(pythonBridge ?? CreatePythonBridge());
        services.AddExecutionServices();
        return services.BuildServiceProvider();
    }

    public static IPythonBridge CreatePythonBridge() => new InlinePythonBridge();

    public static PythonInitializer CreatePythonInitializer(
        PixiEnvironmentProvider? pixi = null,
        UvEnvironmentProvider? uv = null,
        PipEnvironmentProvider? pip = null,
        IPythonBridge? bridge = null)
    {
        return new PythonInitializer(
            pixi ?? new PixiEnvironmentProvider(NullLogger<PixiEnvironmentProvider>.Instance),
            uv ?? new UvEnvironmentProvider(NullLogger<UvEnvironmentProvider>.Instance),
            pip ?? new PipEnvironmentProvider(NullLogger<PipEnvironmentProvider>.Instance),
            bridge ?? CreatePythonBridge(),
            NullLogger<PythonInitializer>.Instance);
    }

    public static async Task<PythonInitializer> EnsurePixiPythonInitializedAsync()
    {
        PythonEmbedded.Configure(HostApp.Revit);
        await PixiInstaller.SetupPixiAsync(NullLogger.Instance);

        var pixi = new PixiEnvironmentProvider(NullLogger<PixiEnvironmentProvider>.Instance);
        if (!pixi.IsEnvironmentReady())
            await pixi.SetupEnvironmentAsync();

        var initializer = CreatePythonInitializer(pixi: pixi);
        await initializer.InitializeAsync();
        EnsureDevtoolNamespace(initializer);
        return initializer;
    }

    /// <summary>
    /// Headless tests use SetupRevit.py which may fail before sys.__devtool__ is created when Revit API DLLs are absent.
    /// </summary>
    public static void EnsureDevtoolNamespace(PythonInitializer initializer)
    {
        if (!PythonEngine.IsInitialized || initializer.GlobalScope is null)
            return;

        using (Py.GIL())
        {
            initializer.GlobalScope.Exec("""
                import sys
                if not hasattr(sys, '__devtool__'):
                    setattr(sys, '__devtool__', {})
                """);
        }
    }

    public static FakeFileWatcherService CreateFileWatcher() => new();

    private sealed class InlinePythonBridge : IPythonBridge
    {
        public string ProgramName => "DevTools.Execution.Tests";

        public void SetupBuiltins(dynamic builtins, PyModule globalScope)
        {
        }
    }

    public static ExecutionNode CreateExecutableNode(
        string id,
        IExecutionStrategy? strategy = null,
        ExecutionMode mode = ExecutionMode.CSharp)
    {
        return new ExecutionNode
        {
            Id = id,
            Name = id,
            ExecutablePath = id,
            SourceFilePath = id,
            ContainerMode = ContainerMode.Script,
            ExecutionMode = mode,
            NodeType = NodeType.Executable,
            ExecutionStrategy = strategy ?? new StubExecutionStrategy(),
        };
    }

    public static ExecutionNodeRoot CreateScriptRoot(string rootPath, params ExecutionNodeBase[] children)
    {
        var root = new ExecutionNodeRoot
        {
            Id = $"root://{rootPath}",
            Name = Path.GetFileName(rootPath),
            RootPath = rootPath,
            ContainerMode = ContainerMode.Script,
            NodeType = NodeType.Container,
            IsExpanded = true,
        };

        foreach (var child in children)
            root.Children.Add(child);

        return root;
    }

    private sealed class InlineHostContextExecutor : IHostContextExecutor
    {
        public Task<T> ExecuteAsync<T>(Func<T> handler, CancellationToken token = default) =>
            Task.FromResult(handler());

        public Task ExecuteAsync(Action action, CancellationToken token = default)
        {
            action();
            return Task.CompletedTask;
        }
    }

    private sealed class InlineCompiledScriptBridge(string commandTypeName) : ICompiledScriptBridge
    {
        public IEnumerable<string> GetSessionReferences() => [];

        public IEnumerable<Assembly> GetParentBindings() => [];

        public Type? TryFindCommandType(Assembly assembly) =>
            assembly.GetTypes().FirstOrDefault(t => t.Name == commandTypeName);

        public string? GetHostReferencePattern() => null;

        public string GetHostReferenceReplacement() => string.Empty;
    }

    private sealed class FakeHostAppInfo : IHostAppInfo
    {
        public HostApp Host => HostApp.Revit;
        public string VersionNumber => "2025";
        public string? VersionBuild => null;
        public int ProcessId => Environment.ProcessId;
    }

    private sealed class StubExecutionStrategy : IExecutionStrategy
    {
        public Task<ExecutionResult> ExecuteAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        {
            progress?.Report("stub");
            return Task.FromResult(ExecutionResult.Succeeded("ok", 1));
        }
    }

    internal sealed class FakeFileWatcherService : IFileWatcherService
    {
        public event EventHandler<FileChangedEventArgs>? FileChanged;

        public void Watch(string path, IEnumerable<string> patterns)
        {
        }

        public void Unwatch(string path)
        {
        }

        public void UnwatchAll()
        {
        }

        public void Raise(FileChangedEventArgs args) => FileChanged?.Invoke(this, args);

        public void Dispose()
        {
        }
    }
}
