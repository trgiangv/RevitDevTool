using DevTools.Testing.Host.Loading;
using Microsoft.Testing.Platform.CommandLine;

namespace DevTools.TUnit.Host.Tests;

public sealed class TUnitGenerationPolicyTests
{
    [Fact]
    public void Policy_pins_tunit_and_mtp_assembly_versions()
    {
        TUnitGenerationPolicy.ValidateMtpAssemblyVersion(typeof(ICommandLineOptions).Assembly.Location);

        var tunit = Assert.Throws<TestingGenerationBuildException>(() =>
            TUnitGenerationPolicy.ValidateTUnitFrameworkVersion(typeof(TUnitGenerationPolicyTests).Assembly.Location));
        Assert.Contains("1.66.27.0", tunit.Message, StringComparison.Ordinal);

        var mtp = Assert.Throws<TestingGenerationBuildException>(() =>
            TUnitGenerationPolicy.ValidateMtpAssemblyVersion(typeof(TUnitGenerationPolicyTests).Assembly.Location));
        Assert.Contains("2.4.0.0", mtp.Message, StringComparison.Ordinal);
    }
}
