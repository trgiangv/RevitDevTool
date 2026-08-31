using DevTools.Execution.External.Testing;

namespace DevTools.Execution.Tests;

public sealed class IpyTestDriverIoPathsTests
{
    [Fact]
    public void CreateDriverIoPaths_ScopesFilesToHostProcessId()
    {
        var driverDir = Path.Combine(Path.GetTempPath(), "ipy-driver-io-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(driverDir);
        try
        {
            var first = IpyTestExecutionService.CreateDriverIoPaths(driverDir, 111);
            var second = IpyTestExecutionService.CreateDriverIoPaths(driverDir, 222);

            Assert.Equal(Path.Combine(driverDir, "request_111.json"), first.RequestPath);
            Assert.Equal(Path.Combine(driverDir, "result_111.json"), first.ResultPath);
            Assert.Equal(Path.Combine(driverDir, "request_222.json"), second.RequestPath);
            Assert.Equal(Path.Combine(driverDir, "result_222.json"), second.ResultPath);
        }
        finally
        {
            try
            {
                Directory.Delete(driverDir, recursive: true);
            }
            catch
            {
                // best effort
            }
        }
    }
}
