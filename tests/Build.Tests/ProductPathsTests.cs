namespace Build.Tests;

[TestClass]
public sealed class ProductPathsTests
{
    private string _root = null!;

    [TestInitialize]
    public void CreateFixture()
    {
        _root = Path.Combine(Path.GetTempPath(), $"product-paths-{Guid.NewGuid():N}");
        Project("source/App/App.csproj", @"..\Lib\Lib.csproj");
        Project("source/Lib/Lib.csproj", @"..\Core\Core.csproj");
        Project("source/Core/Core.csproj");
        Project("source/Other/Other.csproj");
        Project("tests/Lib.Tests/Lib.Tests.csproj", @"..\..\source\lib\Lib.csproj");
        Project("tests/Other.Tests/Other.Tests.csproj", @"..\..\source\Other\Other.csproj");
    }

    [TestCleanup]
    public void DeleteFixture() => Directory.Delete(_root, recursive: true);

    [TestMethod]
    public void Closure_follows_project_references()
    {
        CollectionAssert.AreEqual(
            new[] { "source/App/App.csproj", "source/Core/Core.csproj", "source/Lib/Lib.csproj" },
            ProductPaths.Closure(_root, ["source/App/App.csproj"]).ToArray());
    }

    [TestMethod]
    public void Tests_follow_the_closure()
    {
        CollectionAssert.AreEqual(new[] { "tests/Lib.Tests/Lib.Tests.csproj" }, ProductPaths.TestProjects(_root, Fixture).ToArray());
    }

    [TestMethod]
    public void Paths_cover_closure_and_tests()
    {
        var paths = ProductPaths.Compute(_root, Fixture);

        CollectionAssert.IsSubsetOf(
            new[] { "source/App/**", "source/Core/**", "source/Lib/**", "tests/Lib.Tests/**", "Directory.Packages.props", "build/**" },
            paths.ToArray());
        CollectionAssert.DoesNotContain(paths.ToArray(), "source/Other/**");
        CollectionAssert.DoesNotContain(paths.ToArray(), "tests/Other.Tests/**");
    }

    [TestMethod]
    public void Missing_reference_names_the_project()
    {
        Project("source/Broken/Broken.csproj", @"..\Gone\Gone.csproj");

        var error = Assert.ThrowsExactly<InvalidOperationException>(() => ProductPaths.Closure(_root, ["source/Broken/Broken.csproj"]));
        Assert.Contains("source/Gone/Gone.csproj is missing", error.Message);
    }

    [TestMethod]
    public void Rendered_file_has_every_key()
    {
        var keys = ProductPaths.Render(RepositoryRoot)
            .Split('\n')
            .Where(line => line.EndsWith(':'))
            .ToArray();

        CollectionAssert.AreEqual(new[] { "daemon:", "addin:", "nettest:", "pytest:", "docs:", "workflows:" }, keys);
    }

    [TestMethod]
    public void Committed_paths_file_is_current()
    {
        Assert.IsTrue(ProductPaths.IsCurrent(RepositoryRoot), "Run 'dotnet run --project build -- paths' and commit .github/product-paths.yml.");
    }

    [TestMethod]
    [DataRow("source/DevTools.Daemon/**", "source/DevTools.Daemon/Program.cs", true)]
    [DataRow("tests/*", "tests/Directory.Build.props", true)]
    [DataRow("tests/*", "tests/X.Tests/A.cs", false)]
    [DataRow("global.json", "global.json", true)]
    [DataRow("props/AddinVersion.props", "props/AddinVersion.props.bak", false)]
    public void Glob_matches_like_paths_filter(string pattern, string path, bool match)
    {
        Assert.AreEqual(match, PathGlob.IsMatch(pattern, path));
    }

    private Product Fixture => Product.Daemon with
    {
        Roots = ["source/App/App.csproj"],
        VersionFile = "source/App/App.csproj",
        Changelog = "source/App/CHANGELOG.md"
    };

    private static string RepositoryRoot
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (!File.Exists(Path.Combine(directory.FullName, "RevitDevTool.slnx"))) directory = directory.Parent!;
            return directory.FullName;
        }
    }

    private void Project(string path, params string[] references)
    {
        var full = Path.Combine(_root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var items = string.Concat(references.Select(reference => $"<ProjectReference Include=\"{reference}\" />"));
        File.WriteAllText(full, $"<Project Sdk=\"Microsoft.NET.Sdk\"><ItemGroup>{items}</ItemGroup></Project>");
    }
}
