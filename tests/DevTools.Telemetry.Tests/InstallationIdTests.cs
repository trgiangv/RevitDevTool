using DevTools.Telemetry;

namespace DevTools.Telemetry.Tests;

public sealed class InstallationIdTests
{
    [Fact]
    public void GetOrCreate_returns_stable_guid_on_repeat_calls()
    {
        var first = InstallationId.GetOrCreate();
        var second = InstallationId.GetOrCreate();
        Assert.Equal(first, second);
        Assert.True(Guid.TryParse(first, out _));
    }
}
