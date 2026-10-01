namespace DevTools.MSTest.SampleTests;

[TestClass]
public sealed class NestedCapabilityTests
{
    [TestClass]
    public sealed class Inner
    {
        [TestMethod]
        public void Nested_class_is_discovered()
        {
            Assert.IsTrue(XYZ.Zero.IsZeroLength());
        }
    }
}
