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
        if (!AdapterSettings.IsConfigured)
            return;

        foreach (var source in sources)
        {
            try
            {
                SetWorkingDirectory(source);
                foreach (var test in LocalNUnitTestDiscoverer.Discover(source))
                    discoverySink.SendTestCase(VSTestCaseMapper.ToTestCase(test));
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
