using System.ComponentModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace DevTools.NUnit.TestAdapter;

[DefaultExecutorUri(DevToolsNUnitConstants.ExecutorUri)]
[FileExtension(".dll")]
[Category("managed")]
[UsedImplicitly]
public sealed class DevToolsNUnitDiscoverer : ITestDiscoverer
{
    public void DiscoverTests(
        IEnumerable<string> sources,
        IDiscoveryContext discoveryContext,
        IMessageLogger messageLogger,
        ITestCaseDiscoverySink discoverySink)
    {
        AdapterSettings.Apply(discoveryContext.RunSettings);

        foreach (var source in sources)
        {
            try
            {
                AdapterSettings.TryApplyFromAssembly(source);
                if (!AdapterSettings.IsConfigured)
                    continue;

                SetWorkingDirectory(source);
                foreach (var test in LocalNUnitTestDiscoverer.Discover(source))
                    discoverySink.SendTestCase(VsTestCaseMapper.ToTestCase(test));
            }
            catch (Exception ex)
            {
                messageLogger.SendMessage(TestMessageLevel.Error, $"DevTools.NUnit discovery failed for '{source}': {ex.Message}");
            }
        }
    }

    private static void SetWorkingDirectory(string source)
    {
        var directory = Path.GetDirectoryName(source);
        if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
            Directory.SetCurrentDirectory(directory);
    }
}
