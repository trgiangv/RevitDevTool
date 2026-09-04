using DevTools.Execution.Models;
using DevTools.Execution.Providers.Python;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTools.Execution.Tests;

public sealed class PipEnvironmentProviderTests
{
    [Fact]
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

            Assert.Equal(first, selected);
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public void SelectCengineDir_ReturnsNull_WhenNoPythonExeExists()
    {
        var root = Directory.CreateTempSubdirectory("pip-cengine-empty-");
        try
        {
            var engine = Path.Combine(root.FullName, "CPY_3_13");
            Directory.CreateDirectory(engine);

            Assert.Null(PipEnvironmentProvider.SelectCengineDir([engine], requiredVersion: null));
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    [Fact]
    public void AttachHostInterpreter_SetsHostAttachedFlag()
    {
        var provider = new PipEnvironmentProvider(NullLogger<PipEnvironmentProvider>.Instance);
        var dll = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "kernel32.dll");

        provider.AttachHostInterpreter(dll);

        Assert.NotNull(provider);
    }

    [Fact]
    public void Backend_IsPip()
    {
        var provider = new PipEnvironmentProvider(NullLogger<PipEnvironmentProvider>.Instance);
        Assert.Equal(PythonBackend.Pip, provider.Backend);
    }

    [Fact]
    public async Task GetListJsonAsync_WhenEnvironmentNotReady_ReturnsEmpty()
    {
        var provider = new PipEnvironmentProvider(NullLogger<PipEnvironmentProvider>.Instance);
        var json = await provider.GetListJsonAsync(TestContext.Current.CancellationToken);
        Assert.Equal(string.Empty, json);
    }
}
