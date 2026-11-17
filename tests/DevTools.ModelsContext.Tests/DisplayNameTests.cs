using DevTools.ModelsContext.Model;

namespace DevTools.ModelsContext.Tests;

[TestClass]
public sealed class DisplayNameTests
{
    [TestMethod]
    public void LabelIsFileName()
    {
        Assert.AreEqual("Snowdon Towers Sample HVAC.rvt",
            new SourceId(@"model:C:\Samples\Snowdon Towers Sample HVAC.rvt").Label);
        Assert.AreEqual("architectural.rvt", new SourceId(@"link:c:\samples\architectural.rvt").Label);
        Assert.AreEqual("Project1", new SourceId("model:unsaved:Project1").Label);
        Assert.AreEqual("current", new SourceId("current").Label);
    }

    [TestMethod]
    public void ItemsNameThemselves()
    {
        Assert.AreEqual("L2", new ValueCount("L2", 16).ToString());
    }
}
