using DevTools.AssemblyIsolation.Diagnostics;

namespace DevTools.AssemblyIsolation.Tests;

[TestClass]
public sealed class AssemblyIsolationDiagnosticTests
{
    [TestMethod]
    [DataRow("managed-candidate-rejected", true)]
    [DataRow("native-candidate-rejected", true)]
    [DataRow("managed-candidate-selected", false)]
    [DataRow("native-candidate-selected", false)]
    [DataRow("identity-already-loaded", false)]
    [DataRow("path-changed", false)]
    public void Rejection_is_known_by_code(string code, bool expected) =>
        Assert.AreEqual(expected, new AssemblyIsolationDiagnostic(code, "message").IsRejection);
}
