using DevTools.Execution.Providers.Python;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevTools.Execution.Tests;

public sealed class UvHostCaptureTests
{
    [Fact]
    public void AttachHostInterpreter_ReadsMinorFromVersionedDll()
    {
        var provider = new UvEnvironmentProvider(NullLogger<UvEnvironmentProvider>.Instance, () => null);
        provider.AttachHostInterpreter(@"C:\Host\PLNT3D\python313.dll");

        Assert.Equal("3.13", provider.BoundPythonVersion);
    }

    [Fact]
    public void ResolveBoundVersion_ForwarderUsesSiblingVersionedDll()
    {
        var dir = Path.Combine(Path.GetTempPath(), "rdt-uv-ver-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var forwarder = Path.Combine(dir, "python3.dll");
            var versioned = Path.Combine(dir, "python313.dll");
            File.WriteAllBytes(forwarder, [1]);
            File.WriteAllBytes(versioned, [1]);

            Assert.Equal("3.13", UvEnvironmentProvider.ResolveBoundVersion(forwarder));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void AttachHostInterpreter_DoesNotUseLiveProbe()
    {
        var probeCount = 0;
        var provider = new UvEnvironmentProvider(
            NullLogger<UvEnvironmentProvider>.Instance,
            () =>
            {
                probeCount++;
                return "3.14";
            });

        provider.AttachHostInterpreter(@"C:\Host\python313.dll");
        _ = provider.BoundPythonVersion;

        Assert.Equal("3.13", provider.BoundPythonVersion);
        Assert.Equal(0, probeCount);
    }

    [Fact]
    public void IsVenvRunnable_RequiresPyvenvHomePython()
    {
        var root = Path.Combine(Path.GetTempPath(), "rdt-uv-venv-" + Guid.NewGuid().ToString("N"));
        var venv = Path.Combine(root, "venv");
        var prefix = Path.Combine(root, "cpython");
        Directory.CreateDirectory(Path.Combine(venv, "Scripts"));
        Directory.CreateDirectory(prefix);
        File.WriteAllBytes(Path.Combine(venv, "Scripts", "python.exe"), [1]);
        File.WriteAllText(Path.Combine(venv, "pyvenv.cfg"), $"home = {prefix}{Environment.NewLine}");
        try
        {
            Assert.False(UvEnvironmentProvider.IsVenvRunnable(venv));
            File.WriteAllBytes(Path.Combine(prefix, "python.exe"), [1]);
            Assert.True(UvEnvironmentProvider.IsVenvRunnable(venv));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
