using System.Diagnostics.CodeAnalysis;

namespace Build.Products;

public enum VersionGrammar
{
    SemVer,
    Pep440
}

public sealed record Product(
    string Name,
    string[] Roots,
    string[] ExtraPaths,
    string VersionFile,
    string Changelog,
    VersionGrammar Grammar,
    string[] Artifacts) : ISpanParsable<Product>
{
    public static Product Daemon { get; } = new(
        "daemon",
        ["source/DevTools.Daemon/DevTools.Daemon.csproj"],
        [],
        "source/DevTools.Daemon/DevTools.Daemon.csproj",
        "source/DevTools.Daemon/CHANGELOG.md",
        VersionGrammar.SemVer,
        ["RevitDevTool-Setup.exe", "RevitDevTool-{0}-*full.nupkg", "RevitDevTool-{0}-*delta.nupkg", "releases.<channel>.json", "assets.<channel>.json"]);

    public static Product Addin { get; } = new(
        "addin",
        ["source/RevitDevTool/RevitDevTool.csproj", "source/AcadDevTool/ACadDevTool.csproj"],
        ["props/PackageContents.xml", "source/RevitDevTool.PyTest/tests/golden/**"],
        "props/AddinVersion.props",
        "CHANGELOG.md",
        VersionGrammar.SemVer,
        ["RevitDevTool-{0}-<year>.zip", "update-manifest.json", "RevitDevTool.bundle.zip"]);

    public static Product NetTest { get; } = new(
        "nettest",
        [
            "source/DevTools.TestAdapter/DevTools.TestAdapter.csproj",
            "source/DevTools.TestRunner/DevTools.TestRunner.csproj",
            "source/DevTools.NUnit.MTP/DevTools.NUnit.MTP.csproj",
            "source/DevTools.NUnit.Runtime/DevTools.NUnit.Runtime.csproj",
            "source/DevTools.TUnit.MTP/DevTools.TUnit.MTP.csproj",
            "source/DevTools.TUnit.Runtime/DevTools.TUnit.Runtime.csproj",
            "source/DevTools.MSTest.MTP/DevTools.MSTest.MTP.csproj",
            "source/DevTools.MSTest.Runtime/DevTools.MSTest.Runtime.csproj"
        ],
        ["props/TestingRuntime.props"],
        "source/DevTools.TestAdapter/DevTools.TestAdapter.csproj",
        "source/DevTools.TestAdapter/CHANGELOG.md",
        VersionGrammar.SemVer,
        ["RevitDevTool.TestAdapter.{0}.nupkg"]);

    public static Product PyTest { get; } = new(
        "pytest",
        [],
        ["source/RevitDevTool.PyTest/**"],
        "source/RevitDevTool.PyTest/pyproject.toml",
        "source/RevitDevTool.PyTest/CHANGELOG.md",
        VersionGrammar.Pep440,
        ["revitdevtool_pytest-{0}-py3-none-any.whl", "revitdevtool_pytest-{0}.tar.gz"]);

    public static IReadOnlyList<Product> All { get; } = [Daemon, Addin, NetTest, PyTest];

    public string TagPrefix => $"{Name}/v";

    public string Tag(string version) => TagPrefix + version;

    public bool IsDotNet => Roots.Length > 0;

    public string Output(string root) => Path.Combine(root, "output", Name);

    public static Product? Find(string name) =>
        All.FirstOrDefault(product => string.Equals(product.Name, name, StringComparison.Ordinal));

    public static Product Parse(string s, IFormatProvider? provider) => Parse(s.AsSpan(), provider);

    public static Product Parse(ReadOnlySpan<char> s, IFormatProvider? provider) =>
        TryParse(s, provider, out var product) ? product : throw new FormatException($"Unknown product '{s}'; use daemon, addin, nettest or pytest.");

    public static bool TryParse(string? s, IFormatProvider? provider, [NotNullWhen(true)] out Product? result) =>
        TryParse(s.AsSpan(), provider, out result);

    public static bool TryParse(ReadOnlySpan<char> s, IFormatProvider? provider, [NotNullWhen(true)] out Product? result)
    {
        result = Find(s.ToString());
        return result is not null;
    }
}
