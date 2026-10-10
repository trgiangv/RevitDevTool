using DevTools.Testing.Host.TUnit;
using DevTools.Testing.Host.Loading;
using Microsoft.Testing.Platform.CommandLine;

namespace DevTools.TUnit.Host.Tests;

[TestClass]
public sealed class TUnitGenerationPolicyTests
{
    [TestMethod]
    public void Policy_pins_tunit_and_mtp_assembly_versions()
    {
        ValidatePin("Microsoft.Testing.Platform.dll", typeof(ICommandLineOptions).Assembly.Location);

        var tunit = Assert.ThrowsExactly<TestingGenerationBuildException>(() =>
            ValidatePin("TUnit.Core.dll", typeof(TUnitGenerationPolicyTests).Assembly.Location));
        Assert.Contains("1.73.19.0", tunit.Message, StringComparison.Ordinal);

        var mtp = Assert.ThrowsExactly<TestingGenerationBuildException>(() =>
            ValidatePin("Microsoft.Testing.Platform.dll", typeof(TUnitGenerationPolicyTests).Assembly.Location));
        Assert.Contains("2.5.1.0", mtp.Message, StringComparison.Ordinal);
    }

    private static void ValidatePin(string fileName, string path) =>
        TUnitGenerationPolicy.Spec.Pins.Single(pin => pin.FileName == fileName).Validate(path, null);
}
