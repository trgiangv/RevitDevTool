using System.Reflection;

namespace DevTools.Utilities.Tests;

[TestClass]
public sealed class AppVersionTests
{
    [TestMethod]
    public void Version_is_the_informational_version_without_build_metadata()
    {
        var informational = typeof(AppVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion;

        Assert.DoesNotContain("+", AppVersion.Current, StringComparison.Ordinal);
        Assert.StartsWith(AppVersion.Current, informational, StringComparison.Ordinal);
    }
}
