using DevTools.Execution.Providers.Python;

namespace DevTools.Execution.Tests;

public sealed class PipCengineTests
{
    [Fact]
    public void SelectCengineDir_HostSidecar_SameVersionOnly()
    {
        var root = Path.Combine(Path.GetTempPath(), "rdt-cpy-" + Guid.NewGuid().ToString("N"));
        var cpy312 = Path.Combine(root, "CPY3123");
        var cpy313 = Path.Combine(root, "CPY3130");
        Directory.CreateDirectory(cpy312);
        Directory.CreateDirectory(cpy313);
        File.WriteAllBytes(Path.Combine(cpy312, "python.exe"), [1]);
        File.WriteAllBytes(Path.Combine(cpy313, "python.exe"), [1]);
        File.WriteAllBytes(Path.Combine(cpy312, "python312.dll"), [1]);
        File.WriteAllBytes(Path.Combine(cpy313, "python313.dll"), [1]);
        try
        {
            Assert.Equal(cpy313, PipEnvironmentProvider.SelectCengineDir([cpy312, cpy313], "3.13"));
            Assert.Null(PipEnvironmentProvider.SelectCengineDir([cpy312], "3.13"));
            Assert.Equal(cpy312, PipEnvironmentProvider.SelectCengineDir([cpy312, cpy313], requiredVersion: null));

            var forwarderOnly = Path.Combine(root, "CPY3FWD");
            Directory.CreateDirectory(forwarderOnly);
            File.WriteAllBytes(Path.Combine(forwarderOnly, "python.exe"), [1]);
            File.WriteAllBytes(Path.Combine(forwarderOnly, "python3.dll"), [1]);
            Assert.Null(PipEnvironmentProvider.SelectCengineDir([forwarderOnly], "3.13"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
