namespace Build.Trains;

public enum VersionGrammar
{
    SemVer,
    Pep440
}

public sealed record Train(
    string Name,
    string[] Roots,
    string[] ExtraPaths,
    string VersionFile,
    string Changelog,
    VersionGrammar Grammar,
    string[] Artifacts)
{
    public static Train Daemon { get; } = new(
        "daemon",
        ["source/DevTools.Daemon/DevTools.Daemon.csproj"],
        [],
        "source/DevTools.Daemon/DevTools.Daemon.csproj",
        "source/DevTools.Daemon/CHANGELOG.md",
        VersionGrammar.SemVer,
        ["RevitDevTool-Setup.exe", "RevitDevTool-{0}-*full.nupkg", "RevitDevTool-{0}-*delta.nupkg", "releases.<channel>.json", "assets.<channel>.json"]);

    public static Train Addin { get; } = new(
        "addin",
        ["source/RevitDevTool/RevitDevTool.csproj", "source/AcadDevTool/ACadDevTool.csproj"],
        ["props/PackageContents.xml", "source/RevitDevTool.PyTest/tests/golden/**"],
        "source/AddinVersion.props",
        "source/RevitDevTool/CHANGELOG.md",
        VersionGrammar.SemVer,
        ["RevitDevTool-{0}-<year>.zip", "update-manifest.json", "RevitDevTool.bundle.zip"]);

    public static Train NetTest { get; } = new(
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

    public static Train PyTest { get; } = new(
        "pytest",
        [],
        ["source/RevitDevTool.PyTest/**"],
        "source/RevitDevTool.PyTest/pyproject.toml",
        "source/RevitDevTool.PyTest/CHANGELOG.md",
        VersionGrammar.Pep440,
        ["revitdevtool_pytest-{0}-py3-none-any.whl", "revitdevtool_pytest-{0}.tar.gz"]);

    public static IReadOnlyList<Train> All { get; } = [Daemon, Addin, NetTest, PyTest];

    public string TagPrefix => $"{Name}/v";

    public string Tag(string version) => TagPrefix + version;

    public bool IsDotNet => Roots.Length > 0;

    public string Output(string root) => Path.Combine(root, "output", Name);

    public static Train? Find(string name) =>
        All.FirstOrDefault(train => string.Equals(train.Name, name, StringComparison.Ordinal));
}
