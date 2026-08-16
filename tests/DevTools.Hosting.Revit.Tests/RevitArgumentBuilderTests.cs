using DevTools.Hosting;
using DevTools.Hosting.Revit;

namespace DevTools.Hosting.Revit.Tests;

public sealed class RevitArgumentBuilderTests
{
    private readonly RevitArgumentBuilder _builder = new();

    [Fact]
    public void Default_culture_maps_to_ENU_and_omits_splash_flags()
    {
        var args = _builder.Build(new HostLaunchRequest(HostApp.Revit, "2025", null, null), @"C:\Revit.exe");
        Assert.Equal(["/language", "ENU"], args);
        Assert.DoesNotContain("/nosplash", args, StringComparer.OrdinalIgnoreCase);
        Assert.DoesNotContain("/nologo", args, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void en_US_maps_to_ENU()
    {
        var args = _builder.Build(
            new HostLaunchRequest(HostApp.Revit, "2025", null, Language("en-US")),
            @"C:\Revit.exe");
        Assert.Equal(["/language", "ENU"], args);
    }

    [Fact]
    public void Unmapped_culture_throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => _builder.Build(
            new HostLaunchRequest(HostApp.Revit, "2025", null, Language("ENU")),
            @"C:\Revit.exe"));
        Assert.Contains("en-US", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Revit ENU", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void File_path_appends_after_language()
    {
        var args = _builder.Build(
            new HostLaunchRequest(HostApp.Revit, "2025", @"C:\model.rvt", null),
            @"C:\Revit.exe");
        Assert.Equal(["/language", "ENU", @"C:\model.rvt"], args);
    }

    private static IReadOnlyDictionary<string, string> Language(string culture) =>
        new Dictionary<string, string> { [HostLaunchRequest.LanguageOptionKey] = culture };
}
