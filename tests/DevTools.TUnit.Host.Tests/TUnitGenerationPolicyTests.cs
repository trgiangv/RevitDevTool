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
        TUnitGenerationPolicy.ValidateMtpAssemblyVersion(typeof(ICommandLineOptions).Assembly.Location);

        var tunit = Assert.ThrowsExactly<TestingGenerationBuildException>(() =>
            TUnitGenerationPolicy.ValidateTUnitFrameworkVersion(typeof(TUnitGenerationPolicyTests).Assembly.Location));
        Assert.Contains("1.67.0.0", tunit.Message, StringComparison.Ordinal);

        var mtp = Assert.ThrowsExactly<TestingGenerationBuildException>(() =>
            TUnitGenerationPolicy.ValidateMtpAssemblyVersion(typeof(TUnitGenerationPolicyTests).Assembly.Location));
        Assert.Contains("2.4.0.0", mtp.Message, StringComparison.Ordinal);
    }
}
