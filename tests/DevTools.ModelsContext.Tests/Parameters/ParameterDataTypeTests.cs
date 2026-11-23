using DevTools.ModelsContext.Filters;
using DevTools.ModelsContext.Parameters;

namespace DevTools.ModelsContext.Tests.Parameters;

[TestClass]
public sealed class ParameterDataTypeTests
{
    private static readonly ParameterKey LengthWidth = ParameterKey.Named("Width", "autodesk.spec.aec:length");

    [TestMethod]
    public void DataTypeDropsVersionSuffix()
    {
        Assert.AreEqual("autodesk.spec.aec:length", ParameterDataTypes.Of("autodesk.spec.aec:length-2.0.0", null));
    }

    [TestMethod]
    public void DataTypeKeepsPlainSpec()
    {
        Assert.AreEqual("autodesk.spec:spec.string", ParameterDataTypes.Of("autodesk.spec:spec.string", null));
    }

    [TestMethod]
    public void DataTypeKeepsDashBeforeColon()
    {
        Assert.AreEqual("my-spec:text", ParameterDataTypes.Of("my-spec:text", null));
    }

    [TestMethod]
    public void DataTypeKeepsNonNumericSuffix()
    {
        Assert.AreEqual("autodesk.spec:spec-beta", ParameterDataTypes.Of("autodesk.spec:spec-beta", null));
    }

    [TestMethod]
    public void DataTypeFallsBackToStorage()
    {
        Assert.AreEqual("Double", ParameterDataTypes.Of(null, "Double"));
        Assert.IsNull(ParameterDataTypes.Of(null, null));
    }

    [TestMethod]
    public void FitsAnyWithoutKeyType()
    {
        Assert.IsTrue(ParameterDataTypes.Fits(ParameterKey.Named("Width", null), "autodesk.spec:spec.string-2.0.0", null));
    }

    [TestMethod]
    public void FitsWhenNothingKnown()
    {
        Assert.IsTrue(ParameterDataTypes.Fits(LengthWidth, null, null));
    }

    [TestMethod]
    public void FitsSameSpecOtherVersion()
    {
        Assert.IsTrue(ParameterDataTypes.Fits(LengthWidth, "autodesk.spec.aec:length-1.0.0", null));
    }

    [TestMethod]
    public void OtherSpecDoesNotFit()
    {
        Assert.IsFalse(ParameterDataTypes.Fits(LengthWidth, "autodesk.spec:spec.string-2.0.0", null));
        Assert.IsFalse(ParameterDataTypes.Fits(LengthWidth, null, "String"));
    }

    [TestMethod]
    public void NamedValueIsText()
    {
        Assert.IsTrue(ParameterDataTypes.IsNamedValue("Single"));
        Assert.IsTrue(ParameterDataTypes.IsNamedValue("By Category"));
    }

    [TestMethod]
    public void NumberIsNotNamedValue()
    {
        Assert.IsFalse(ParameterDataTypes.IsNamedValue("42"));
        Assert.IsFalse(ParameterDataTypes.IsNamedValue("1.5"));
        Assert.IsFalse(ParameterDataTypes.IsNamedValue(" "));
        Assert.IsFalse(ParameterDataTypes.IsNamedValue(null));
    }
}
