using System.Reflection;
using System.Text;
using DevTools.MSTest.MTP;
using DevTools.Testing.Abstractions.Contracts;
using DevTools.Testing.Abstractions.Runtime;
using Microsoft.Testing.Platform.Builder;

namespace DevTools.MSTest.Runtime;

public sealed class MSTestRuntimeSession : CancellableRuntimeSession
{
    private const string PlatformConfig =
        "{\"mstest\":{\"parallelism\":{\"enabled\":false},\"output\":{\"captureTrace\":false}}}";

    private readonly Assembly _testAssembly;

    public MSTestRuntimeSession(Assembly testAssembly, string assemblyPath, string generationId)
        : base("MSTest", assemblyPath, generationId)
    {
        ArgumentNullException.ThrowIfNull(testAssembly);
        _testAssembly = testAssembly;
    }

    protected override TestRunResponse Execute(
        TestRunRequest request,
        ITestEventSink eventSink,
        CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return CreateCancelledResponse(request);

        switch (request.Selection.Kind)
        {
            case TestSelectionKind.FrameworkFilter or TestSelectionKind.Names:
                return CreateInvalidSelectionResponse(request, "MSTest does not accept that selection.");
            case TestSelectionKind.TestIds when request.Selection.TestIds.Count == 0:
                return CreateEmptyResponse(request);
            default:
            {
                var selection = WithDependencies(request.Selection);
                return RunApplication(request, selection, eventSink, cancellationToken).GetAwaiter().GetResult();
            }
        }

    }

    /// <summary>
    /// MSTest orders <c>[DependsOn]</c> only among tests that are already in the run.
    /// A single selected id therefore runs with <c>ChainStep</c> still 0 unless its
    /// prerequisites are added here, before <c>--filter-uid</c>.
    /// </summary>
    private TestSelection WithDependencies(TestSelection selection)
    {
        if (selection.Kind != TestSelectionKind.TestIds)
            return selection;

        var listed = MSTestListing.List(_testAssembly);
        return TestSelection.FromTestIds(
            MSTestExpansion.Expand(_testAssembly, listed, selection.TestIds).ToList());
    }
    private async Task<TestRunResponse> RunApplication(
        TestRunRequest request,
        TestSelection selection,
        ITestEventSink eventSink,
        CancellationToken cancellationToken)
    {
        MSTestPlatformPolicy.EnsurePinnedVersions();
        // MTP keeps one process-wide metadata hook; the last generation to register wins.
        MSTestAssemblyRegistration.Register(_testAssembly);

        var configPath = Path.Combine(Path.GetTempPath(), "devtools-mstest-" + request.RunId.ToString("N") + ".testconfig.json");
        var resultsDirectory = Path.Combine(Path.GetTempPath(), "devtools-mstest-" + request.RunId.ToString("N"));
        Directory.CreateDirectory(resultsDirectory);
        await WriteConfigAsync(configPath).ConfigureAwait(false);

        ITestApplication? application = null;
        try
        {
            using var traceScope = new TestRunTraceScope(MSTestConsoleCapture.CurrentDisplayName);
            using var consoleCapture = new MSTestConsoleCapture();
            var builder = await TestApplication.CreateBuilderAsync(
                    BuildArguments(configPath, resultsDirectory, selection),
                    new TestApplicationOptions
                    {
                        EnableTelemetry = false,
                        CancellationToken = cancellationToken,
                    })
                .ConfigureAwait(false);
            builder.AddMSTest(() => [_testAssembly]);
            var consumer = new MSTestNodeConsumer(traceScope, consoleCapture);
            builder.TestHost.AddDataConsumer(_ => consumer);
            application = await builder.BuildAsync().ConfigureAwait(false);

            consoleCapture.Start();
            var exitCode = await application.RunAsync().ConfigureAwait(false);
            var results = MSTestNodeResults.Map(
                consumer.Snapshot(),
                cancellationToken.IsCancellationRequested,
                consumer.CapturedByUid);
            if (exitCode != 0 && results.Count == 0 && !cancellationToken.IsCancellationRequested)
            {
                throw new InvalidOperationException(
                    "MSTest TestApplication.RunAsync returned " + exitCode + " without test results.");
            }

            PublishResults(request.RunId, results, eventSink);
            return CreateCompletedResponse(
                request,
                results,
                cancellationToken.IsCancellationRequested
                    || results.Any(result => result.Outcome == TestOutcomes.Cancelled));
        }
        finally
        {
            if (application is IDisposable disposable)
                disposable.Dispose();
            TryDelete(configPath);
            TryDeleteDirectory(resultsDirectory);
        }
    }

    private static async Task WriteConfigAsync(string configPath)
    {
        using var writer = new StreamWriter(configPath, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        await writer.WriteAsync(PlatformConfig).ConfigureAwait(false);
    }

    private static string[] BuildArguments(string configPath, string resultsDirectory, TestSelection selection)
    {
        var args = new List<string>
        {
            "--config-file",
            configPath,
            "--results-directory",
            resultsDirectory,
        };

        if (selection.Kind == TestSelectionKind.TestIds)
        {
            args.Add("--filter-uid");
            args.AddRange(selection.TestIds.Where(testId => !string.IsNullOrWhiteSpace(testId)));
        }

        return args.ToArray();
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
