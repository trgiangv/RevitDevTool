using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Providers;
using DevTools.Testing.Abstractions.Runtime;
using DevTools.Testing.Host.Loading;
using DevTools.Testing.Host.MSTest;
using DevTools.Testing.Host.Runtime;
using DevTools.Testing.Transport;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;

namespace DevTools.Testing.Host.Tests;

[TestClass]
public sealed class MSTestGenerationPolicyTests
{
    private const string FrameworkNamespace = "Microsoft.VisualStudio.TestTools.UnitTesting";
    private static readonly Version Floor = new(4, 5, 0, 0);
    private static readonly Version Newer = new(4, 6, 0, 0);

    [TestMethod]
    public void Plan_takes_the_runtime_and_framework_from_the_output_only()
    {
        using var workspace = new Workspace();
        workspace.WriteOutput();

        var plan = new MSTestGenerationPolicy().CreatePlan(workspace.ConsumerPath);

        var sources = plan.Files.Select(file => Path.GetDirectoryName(file.SourcePath)).Distinct().ToList();
        CollectionAssert.AreEqual(new[] { workspace.Output }, sources);
        Assert.AreEqual("DevTools.MSTest.Runtime.dll", plan.RuntimeAssemblyRelativePath);
    }

    [TestMethod]
    public void Run_binds_a_newer_user_framework_to_the_runtime_and_activates_the_manifest_entry()
    {
        using var workspace = new Workspace();
        workspace.WriteOutput(found: Newer);
        using var manager = workspace.CreateManager();

        var response = manager.Run(workspace.Request(), NullTestEventSink.Instance);

        Assert.AreEqual(Newer.ToString(), response.DiagnosticMessage);
    }

    [TestMethod]
    public void No_manifest_stops_before_anything_is_published()
    {
        using var workspace = new Workspace();
        workspace.WriteOutput();
        File.Delete(workspace.ManifestPath);
        using var manager = workspace.CreateManager();

        var stop = Assert.ThrowsExactly<TestingRuntimeIncompatibleException>(() =>
            manager.Run(workspace.Request(), NullTestEventSink.Instance));

        Assert.Contains("is not compatible with RevitDevTool", stop.Message, StringComparison.Ordinal);
        Assert.AreEqual(0, workspace.PublishedGenerationCount);
    }

    [TestMethod]
    public void Framework_below_the_floor_stops_before_anything_is_published()
    {
        using var workspace = new Workspace();
        workspace.WriteOutput(found: new Version(Floor.Major, Floor.Minor - 1, 0, 0));
        using var manager = workspace.CreateManager();

        var stop = Assert.ThrowsExactly<TestingRuntimeIncompatibleException>(() =>
            manager.Run(workspace.Request(), NullTestEventSink.Instance));

        Assert.Contains($"was built against. Update MSTest to {Floor.ToString(3)} or later.", stop.Message, StringComparison.Ordinal);
        Assert.AreEqual(0, workspace.PublishedGenerationCount);
    }

    [TestMethod]
    public void Bind_failure_during_activation_is_the_one_sentence_stop()
    {
        using var workspace = new Workspace();
        workspace.WriteOutput(found: Newer, constructorBody: "Gone();", frameworkHasGone: false);
        using var manager = workspace.CreateManager();

        var stop = Assert.ThrowsExactly<TestingRuntimeIncompatibleException>(() =>
            manager.Run(workspace.Request(), NullTestEventSink.Instance));

        Assert.AreEqual(workspace.BindFailureSentence(Newer), stop.Message);
    }

    [TestMethod]
    public void Bind_failure_during_a_run_is_the_one_sentence_stop()
    {
        using var workspace = new Workspace();
        workspace.WriteOutput(found: Newer, runBody: "Gone();", frameworkHasGone: false);
        using var manager = workspace.CreateManager();

        var stop = Assert.ThrowsExactly<TestingRuntimeIncompatibleException>(() =>
            manager.Run(workspace.Request(), NullTestEventSink.Instance));

        Assert.AreEqual(workspace.BindFailureSentence(Newer), stop.Message);
    }

    [TestMethod]
    public void Other_failures_during_a_run_keep_their_type()
    {
        using var workspace = new Workspace();
        workspace.WriteOutput(runBody: "throw new System.InvalidOperationException(\"MSTest.TestFramework failed.\");");
        using var manager = workspace.CreateManager();

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            manager.Run(workspace.Request(), NullTestEventSink.Instance));
    }

    [TestMethod]
    public async Task A_stop_is_ProtocolIncompatible_and_the_next_run_works()
    {
        using var workspace = new Workspace();
        workspace.WriteOutput();
        using var manager = workspace.CreateManager();
        var handler = new TestingRequestHandler(
            new TestingProviderRegistry([new ManagerProvider(manager)]),
            "Revit",
            "2025");
        var manifest = File.ReadAllText(workspace.ManifestPath);
        File.Delete(workspace.ManifestPath);

        var stopped = await handler.HandleAsync("1", TestingProtocol.Run, workspace.RunParams());
        Assert.IsTrue(stopped.IsError);
        Assert.AreEqual(TestingErrorCodes.ProtocolIncompatible, stopped.ErrorDetail!.Code);
        Assert.AreEqual(TestCancellationState.None, handler.CancellationState);

        File.WriteAllText(workspace.ManifestPath, manifest);

        var next = await handler.HandleAsync("2", TestingProtocol.Run, workspace.RunParams());
        Assert.IsFalse(next.IsError, next.ErrorDetail?.Message);
    }

    [TestMethod]
    public void The_isolation_plan_reports_to_the_logger()
    {
        using var workspace = new Workspace();
        workspace.WriteOutput(found: Newer);
        var logger = new RecordingLogger();
        using var manager = workspace.CreateManager(logger);

        manager.Run(workspace.Request(), NullTestEventSink.Instance);

        Assert.IsTrue(logger.Messages.Any(message =>
            message.Contains("managed-candidate-selected", StringComparison.Ordinal)
            && message.Contains("MSTest.TestFramework", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Runtime_built_on_the_shipped_abstractions_runs_on_the_current_abstractions()
    {
        using var workspace = new Workspace();
        workspace.WriteOutput(abstractionsReference: workspace.EmitShippedAbstractions());
        using var manager = workspace.CreateManager();

        var response = manager.Run(workspace.Request(), NullTestEventSink.Instance);

        Assert.AreEqual(TestCancellationState.None, response.CancellationState);
        Assert.AreEqual(Floor.ToString(), response.DiagnosticMessage);
    }

    private sealed class ManagerProvider(TestingRuntimeSessionManager manager) : ITestFrameworkProvider
    {
        public TestFrameworkId FrameworkId => TestFrameworkId.MSTest;

        public TestRunResponse Run(TestRunRequest request, ITestEventSink eventSink, CancellationToken cancellationToken) =>
            manager.Run(request, eventSink, cancellationToken);

        public bool Cancel(Guid runId) => manager.Cancel(runId);
    }

    private sealed class RecordingLogger : ILogger
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }

    private sealed class Workspace : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"mstest-generation-{Guid.NewGuid():N}");

        public Workspace()
        {
            Directory.CreateDirectory(Output);
            Directory.CreateDirectory(References);
            File.Copy(typeof(TestingRequestHandler).Assembly.Location, Path.Combine(Output, "DevTools.TestAdapter.dll"));
            Emit(ConsumerPath, "Consumer", "1.0.0.0", "public sealed class Consumer { }");
        }

        public string Output => Path.Combine(_root, "output");

        public string ConsumerPath => Path.Combine(Output, "Consumer.dll");

        public string ManifestPath => Path.Combine(Output, RuntimeManifest.FileName(TestFrameworkId.MSTest));

        public int PublishedGenerationCount => Directory.Exists(Generations)
            ? Directory.GetDirectories(Generations).Length
            : 0;

        private string References => Path.Combine(_root, "references");

        private string Generations => Path.Combine(_root, "generations");

        public void WriteOutput(
            Version? found = null,
            string constructorBody = "",
            string? runBody = null,
            bool frameworkHasGone = true,
            string? abstractionsReference = null)
        {
            var frameworkFile = "MSTest.TestFramework.dll";
            var reference = Path.Combine(References, frameworkFile);
            Emit(reference, "MSTest.TestFramework", Floor.ToString(), FrameworkSource(Floor, hasGone: true));
            Emit(
                Path.Combine(Output, frameworkFile),
                "MSTest.TestFramework",
                (found ?? Floor).ToString(),
                FrameworkSource(found ?? Floor, frameworkHasGone));

            Emit(
                Path.Combine(Output, "DevTools.MSTest.Runtime.dll"),
                "DevTools.MSTest.Runtime",
                "1.0.0.0",
                RuntimeSource(constructorBody, runBody ?? string.Empty),
                reference,
                abstractionsReference ?? typeof(ITestingRuntimeSession).Assembly.Location);

            File.WriteAllText(
                ManifestPath,
                JsonSerializer.Serialize(
                    new RuntimeManifest(
                        "MSTest",
                        "Fixture.Session",
                        TestingProtocol.RuntimeRange,
                        "MSTest.TestFramework",
                        Floor.ToString()),
                    TestingJsonContext.Default.RuntimeManifest));
        }

        public OutputSessionFactory CreateFactory(ILogger? logger = null) =>
            new(TestFrameworkId.MSTest, logger ?? NullLogger.Instance);

        public TestingRuntimeSessionManager CreateManager(ILogger? logger = null) =>
            new(new TestingGenerationStore(Generations), new MSTestGenerationPolicy(), CreateFactory(logger));

        public TestRunRequest Request() =>
            new(Guid.NewGuid(), TestFrameworkId.MSTest, new TestAssemblyReference(ConsumerPath), TestSelection.All);

        public JsonElement RunParams() =>
            JsonSerializer.SerializeToElement(Request(), TestingJsonContext.Default.TestRunRequest);

        public string BindFailureSentence(Version found) =>
            $"MSTest {found.ToString(3)} changed an API that RevitDevTool.TestAdapter {RuntimeManifest.ReadAdapterVersion(Output)} uses (built against {Floor.ToString(3)}). " +
            $"Update RevitDevTool.TestAdapter, or use MSTest {Floor.ToString(3)}.";

        public string EmitShippedAbstractions()
        {
            var path = Path.Combine(References, "DevTools.Testing.Abstractions.dll");
            Emit(path, "DevTools.Testing.Abstractions", "1.0.0.0", ShippedAbstractionsSource);
            return path;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_root, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }

        private static string FrameworkSource(Version version, bool hasGone) =>
            $$"""
              namespace {{FrameworkNamespace}}
              {
                  public static class Marker
                  {
                      public static string Version() => "{{version}}";
                      {{(hasGone ? "public static void Gone() { }" : string.Empty)}}
                  }
              }
              """;

        private static string RuntimeSource(string constructorBody, string runBody) =>
            $$"""
              using DevTools.Testing.Abstractions.Contracts;
              using DevTools.Testing.Abstractions.Runtime;
              using {{FrameworkNamespace}};

              namespace Fixture
              {
                  public sealed class Session : CancellableRuntimeSession
                  {
                      public Session(System.Reflection.Assembly testAssembly, string assemblyPath, string generationId)
                          : base("MSTest", assemblyPath, generationId)
                      {
                          {{constructorBody}}
                      }

                      protected override TestRunResponse Execute(TestRunRequest request, ITestEventSink eventSink, System.Threading.CancellationToken cancellationToken)
                      {
                          {{runBody}}
                          return new TestRunResponse(request.RunId, request.FrameworkId, GenerationId, new TestCaseResult[0], TestCancellationState.None, null, Marker.Version());
                      }

                      [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
                      private static void Gone() => Marker.Gone();
                  }
              }
              """;

        private const string ShippedAbstractionsSource =
            """
            namespace DevTools.Testing.Abstractions.Contracts
            {
                public enum TestFrameworkId { NUnit, TUnit, MSTest }
                public enum TestCancellationState { None, Requested, Acknowledged, Completed, Poisoned }
                public sealed record TestAssemblyReference(string Path);
                public sealed record TestSelection;
                public sealed record TestEvent;
                public sealed record TestCaseResult;
                public sealed record TestRunRequest
                {
                    public System.Guid RunId { get; init; }
                    public TestFrameworkId FrameworkId { get; init; }
                    public TestAssemblyReference Assembly { get; init; }
                    public TestSelection Selection { get; init; }
                }
                public sealed record TestRunResponse(
                    System.Guid RunId,
                    TestFrameworkId FrameworkId,
                    string GenerationId,
                    System.Collections.Generic.IReadOnlyList<TestCaseResult> Results,
                    TestCancellationState CancellationState,
                    string DiagnosticCode,
                    string DiagnosticMessage);
            }
            namespace DevTools.Testing.Abstractions.Runtime
            {
                using DevTools.Testing.Abstractions.Contracts;
                public interface ITestEventSink { void Publish(TestEvent testingEvent); }
                public interface ITestingRuntimeSession : System.IDisposable
                {
                    string GenerationId { get; }
                    TestRunResponse Run(TestRunRequest request, ITestEventSink eventSink, System.Threading.CancellationToken cancellationToken);
                    void Cancel(System.Guid runId);
                }
                public abstract class CancellableRuntimeSession : ITestingRuntimeSession
                {
                    protected CancellableRuntimeSession(string frameworkName, string assemblyPath, string generationId) => throw null;
                    public string GenerationId => throw null;
                    public TestRunResponse Run(TestRunRequest request, ITestEventSink eventSink, System.Threading.CancellationToken cancellationToken) => throw null;
                    protected abstract TestRunResponse Execute(TestRunRequest request, ITestEventSink eventSink, System.Threading.CancellationToken cancellationToken);
                    public void Cancel(System.Guid runId) => throw null;
                    public void Dispose() => throw null;
                }
            }
            """;

        private static void Emit(string path, string name, string version, string source, params string[] references)
        {
            var platform = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                .Split(Path.PathSeparator)
                .Where(reference => Path.GetFileName(reference).StartsWith("System.", StringComparison.OrdinalIgnoreCase)
                                    || Path.GetFileName(reference) is "netstandard.dll" or "mscorlib.dll");
            var compilation = CSharpCompilation.Create(
                name,
                [CSharpSyntaxTree.ParseText(source), CSharpSyntaxTree.ParseText($"[assembly: System.Reflection.AssemblyVersion(\"{version}\")]")],
                references.Concat(platform).Select(reference => MetadataReference.CreateFromFile(reference)),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Disable));
            using var stream = File.Create(path);
            var result = compilation.Emit(stream);
            if (!result.Success)
                throw new InvalidOperationException(string.Join(Environment.NewLine, result.Diagnostics));
        }
    }
}
