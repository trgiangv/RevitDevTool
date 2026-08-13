using Microsoft.Testing.Platform.Capabilities.TestFramework;

namespace DevTools.NUnit.Mtp;

internal sealed class DevToolsNUnitFrameworkCapabilities : ITestFrameworkCapabilities
{
    public IReadOnlyCollection<ITestFrameworkCapability> Capabilities { get; } = [];
}
