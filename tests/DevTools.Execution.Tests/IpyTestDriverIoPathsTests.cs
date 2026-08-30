using DevTools.Execution.External.Testing;

namespace DevTools.Execution.Tests;

public sealed class IpyTestDriverIoPathsTests
{
    [Fact]
    public void CreateDriverIoPaths_ProducesDistinctFilesPerCall()
    {
        var driverDir = Path.Combine(Path.GetTempPath(), "ipy-driver-io-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(driverDir);
        try
        {
            var first = IpyTestExecutionService.CreateDriverIoPaths(driverDir);
            var second = IpyTestExecutionService.CreateDriverIoPaths(driverDir);

            Assert.NotEqual(first.RequestPath, second.RequestPath);
            Assert.NotEqual(first.ResultPath, second.ResultPath);
            Assert.StartsWith(driverDir, first.RequestPath, StringComparison.Ordinal);
            Assert.StartsWith(driverDir, second.ResultPath, StringComparison.Ordinal);
            Assert.Contains("request_", first.RequestPath, StringComparison.Ordinal);
            Assert.Contains("result_", first.ResultPath, StringComparison.Ordinal);
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
