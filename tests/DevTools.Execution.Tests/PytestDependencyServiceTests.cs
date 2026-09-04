using DevTools.Execution.External.Testing;
using DevTools.Execution.Providers.Python;
using DevTools.Hosting;

namespace DevTools.Execution.Tests;

[Collection(nameof(PythonRuntimeCollection))]
public sealed class PytestDependencyServiceTests
{
    public PytestDependencyServiceTests()
    {
        PythonEmbedded.Configure(HostApp.Revit);
    }

    [Fact]
    public async Task PrepareRunAsync_InitializesPythonWhenNeeded()
    {
        var service = new PytestDependencyService(ExecutionTestHelpers.CreatePythonInitializer());
        var workspace = ExecutionTestHelpers.CreateTempDirectory("pytest-deps-init");
        try
        {
            var request = new PytestRunRequest(workspace, workspace, ["tests/sample.py::test_x"], []);

            await service.PrepareRunAsync(request, TestContext.Current.CancellationToken);
        }
        finally
        {
            TryDeleteDirectory(workspace);
        }
    }

    [Fact]
    public async Task PrepareRunAsync_MissingTestFile_DoesNotThrow()
    {
        var initializer = await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();
        var service = new PytestDependencyService(initializer);
        var workspace = ExecutionTestHelpers.CreateTempDirectory("pytest-deps-missing");
        try
        {
            var request = new PytestRunRequest(
                workspace,
                workspace,
                ["tests/missing.py::TestCase::test_x"],
                []);

            await service.PrepareRunAsync(request, TestContext.Current.CancellationToken);
        }
        finally
        {
            TryDeleteDirectory(workspace);
        }
    }

    [Fact]
    public async Task PrepareRunAsync_WithConftestChain_ResolvesDependencies()
    {
        var initializer = await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();
        var service = new PytestDependencyService(initializer);
        var workspace = ExecutionTestHelpers.CreateTempDirectory("pytest-deps-chain");
        var testsDir = Path.Combine(workspace, "tests", "nested");
        Directory.CreateDirectory(testsDir);

        var conftestPath = Path.Combine(workspace, "tests", "conftest.py");
        await File.WriteAllTextAsync(conftestPath, "# conftest", TestContext.Current.CancellationToken);

        var testPath = Path.Combine(testsDir, "sample.py");
        await File.WriteAllTextAsync(testPath, "def test_ok(): pass", TestContext.Current.CancellationToken);

        try
        {
            var request = new PytestRunRequest(
                workspace,
                Path.Combine(workspace, "tests"),
                ["tests/nested/sample.py::test_ok"],
                []);

            await service.PrepareRunAsync(request, TestContext.Current.CancellationToken);
        }
        finally
        {
            TryDeleteDirectory(workspace);
        }
    }

    [Fact]
    public async Task PrepareRunAsync_WithPep723TestFile_ResolvesInlineDeps()
    {
        var initializer = await ExecutionTestHelpers.EnsurePixiPythonInitializedAsync();
        var service = new PytestDependencyService(initializer);
        var workspace = ExecutionTestHelpers.CreateTempDirectory("pytest-deps-pep723");
        var testsDir = Path.Combine(workspace, "tests");
        Directory.CreateDirectory(testsDir);

        var fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "pep723_no_deps.py");
        var testPath = Path.Combine(testsDir, "pep723_test.py");
        File.Copy(fixture, testPath, overwrite: true);

        try
        {
            var request = new PytestRunRequest(
                workspace,
                testsDir,
                ["tests/pep723_test.py"],
                []);

            await service.PrepareRunAsync(request, TestContext.Current.CancellationToken);
        }
        finally
        {
            TryDeleteDirectory(workspace);
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // best effort
        }
    }
}
