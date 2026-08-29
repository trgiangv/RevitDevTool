using DevTools.TestAdapter;

namespace DevTools.TestAdapter.Tests;

public sealed class AdapterBootstrapTests
{
    [Fact]
    public void Initialize_with_framework_id_only_does_not_throw()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "testconfig.json");
        var previous = File.Exists(configPath) ? File.ReadAllText(configPath) : null;
        File.WriteAllText(configPath, """{"devtools":{"frameworkId":"nunit"}}""");
        try
        {
            var exception = Record.Exception(AdapterBootstrap.Initialize);
            Assert.Null(exception);
            Assert.NotNull(HostMtpRegistration.LastError);
            Assert.Contains("mtpAssembly", HostMtpRegistration.LastError!, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (previous is null)
                File.Delete(configPath);
            else
                File.WriteAllText(configPath, previous);
        }
    }
}
