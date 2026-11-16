using DevTools.UI.Converters;
using System.Globalization;
using System.Windows.Data;

namespace DevTools.HandyControl.Tests;

[TestClass]
public sealed class EnumDescriptionConverterTests
{
    private enum Sample
    {
        [System.ComponentModel.Description("first one")]
        First,
        Second
    }

    private static readonly EnumDescriptionConverter Converter = new();

    [TestMethod]
    public void UsesDescription()
    {
        Assert.AreEqual("first one", Converter.Convert(Sample.First, typeof(string), null, CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void FallsBackToName()
    {
        Assert.AreEqual("Second", Converter.Convert(Sample.Second, typeof(string), null, CultureInfo.InvariantCulture));
    }

    [TestMethod]
    public void ConvertBackDoesNothing()
    {
        Assert.AreSame(Binding.DoNothing, Converter.ConvertBack("first one", typeof(Sample), null, CultureInfo.InvariantCulture));
    }
}
