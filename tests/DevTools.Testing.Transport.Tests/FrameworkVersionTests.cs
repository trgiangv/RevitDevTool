namespace DevTools.Testing.Transport.Tests;

[TestClass]
public sealed class FrameworkVersionTests
{
    [TestMethod]
    [DataRow("5", "5.0.0")]
    [DataRow("5.0.0.0", "5.0.0")]
    [DataRow("1.78.0", "1.78.0")]
    [DataRow("5.0.0.2", "5.0.0.2")]
    [DataRow("4.5.2+933377f", "4.5.2")]
    [DataRow("5.0.0-beta.1", "5.0.0-beta.1")]
    [DataRow("5.0.0-beta.1+abc", "5.0.0-beta.1")]
    public void Parse_and_display(string text, string expected)
    {
        Assert.AreEqual(expected, FrameworkVersion.Parse(text).ToString());
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("five")]
    [DataRow("5.0.0.0.0")]
    [DataRow("5.-1")]
    [DataRow("5.0.0-")]
    [DataRow("5..0")]
    public void Invalid_text_does_not_parse(string? text)
    {
        Assert.IsFalse(FrameworkVersion.TryParse(text, out _));
    }

    [TestMethod]
    public void Short_forms_pad_to_four_parts()
    {
        Assert.AreEqual(FrameworkVersion.Parse("5.0.0.0"), FrameworkVersion.Parse("5.0.0"));
        Assert.AreEqual(FrameworkVersion.Parse("5"), FrameworkVersion.From(new Version(5, 0)));
        Assert.AreEqual(new Version(5, 0, 0, 0), FrameworkVersion.Parse("5").Numbers);
        Assert.AreEqual(0, FrameworkVersion.Parse("5.0.0.0").CompareTo(FrameworkVersion.Parse("5.0.0")));
    }

    [TestMethod]
    public void Prerelease_sorts_below_release()
    {
        var beta = FrameworkVersion.Parse("5.0.0-beta.1");
        var release = FrameworkVersion.Parse("5.0.0");

        Assert.IsTrue(beta < release);
        Assert.IsTrue(release > beta);
        Assert.IsTrue(beta > FrameworkVersion.Parse("4.9.9"));
        Assert.IsTrue(FrameworkVersion.Parse("5.0.0-alpha") < beta);
    }

    [TestMethod]
    public void Numbers_compare_before_labels()
    {
        Assert.IsTrue(FrameworkVersion.Parse("5.0.1-beta") > FrameworkVersion.Parse("5.0.0"));
        Assert.IsTrue(FrameworkVersion.Parse("5.0.0.1") > FrameworkVersion.Parse("5.0.0"));
        Assert.IsTrue(FrameworkVersion.Parse("1.78.0") >= FrameworkVersion.Parse("1.78"));
        Assert.IsTrue(FrameworkVersion.Parse("1.77.9") <= FrameworkVersion.Parse("1.78"));
    }

    [TestMethod]
    public void Build_metadata_is_ignored()
    {
        Assert.AreEqual(FrameworkVersion.Parse("4.5.2"), FrameworkVersion.Parse("4.5.2+933377f"));
    }

    [TestMethod]
    public void Major_is_the_first_part()
    {
        Assert.AreEqual(5, FrameworkVersion.Parse("5.1.0-rc.2").Major);
    }
}
