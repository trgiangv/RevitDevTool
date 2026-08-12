using NUnit.Framework;

namespace DevTools.NUnit.Runtime.Tests.Fixtures;

[TestFixture]
public sealed class AttachmentFixture
{
    [Test]
    public void CreatesAttachmentAndWarning()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".txt");
        File.WriteAllText(path, "attachment-content");
        TestContext.AddTestAttachment(path, "acceptance-attachment");
        Assert.Warn("acceptance-warning-text");
        TestContext.Out.WriteLine("attachment-output-marker");
    }
}
