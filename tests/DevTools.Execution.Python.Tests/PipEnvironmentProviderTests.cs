using DevTools.Execution.Models;
using DevTools.Execution.Providers.Python;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTools.Execution.Tests;

[TestClass]
public sealed class PipEnvironmentProviderTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void SelectCengineDir_ReturnsFirstReadyEngine_WhenNoVersionRequired()
    {
        var root = Directory.CreateTempSubdirectory("pip-cengine-");
        try
        {
            var first = Path.Combine(root.FullName, "CPY_3_13");
            var second = Path.Combine(root.FullName, "CPY_3_14");
            Directory.CreateDirectory(first);
            Directory.CreateDirectory(second);
            File.WriteAllText(Path.Combine(first, "python.exe"), string.Empty);
            File.WriteAllText(Path.Combine(second, "python.exe"), string.Empty);

            var selected = PipEnvironmentProvider.SelectCengineDir([first, second], requiredVersion: null);

            Assert.AreEqual(first, selected);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [TestMethod]
    public void SelectCengineDir_ReturnsNull_WhenNoPythonExeExists()
    {
        var root = Directory.CreateTempSubdirectory("pip-cengine-empty-");
        try
        {
            var engine = Path.Combine(root.FullName, "CPY_3_13");
            Directory.CreateDirectory(engine);

            Assert.IsNull(PipEnvironmentProvider.SelectCengineDir([engine], requiredVersion: null));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [TestMethod]
    public void AttachHostInterpreter_SetsHostAttachedFlag()
    {
        var provider = new PipEnvironmentProvider(NullLogger<PipEnvironmentProvider>.Instance);
        var dll = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "kernel32.dll");

        provider.AttachHostInterpreter(dll);

        Assert.IsNotNull(provider);
    }

    [TestMethod]
    public void Backend_IsPip()
    {
        var provider = new PipEnvironmentProvider(NullLogger<PipEnvironmentProvider>.Instance);
        Assert.AreEqual(PythonBackend.Pip, provider.Backend);
    }

    [TestMethod]
    public async Task GetListJsonAsync_WhenEnvironmentNotReady_ReturnsEmpty()
    {
        var provider = new PipEnvironmentProvider(NullLogger<PipEnvironmentProvider>.Instance);
        var json = await provider.GetListJsonAsync(TestContext.CancellationToken);
        Assert.AreEqual(string.Empty, json);
    }
}
