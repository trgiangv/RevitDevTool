
namespace Build.Tests;

[TestClass]
public sealed class ChangelogNotesTests
{
    private const string Block = "## [Unreleased]\n\n### Added\n- New thing\n";

    [TestMethod]
    public void Replaces_unreleased_block()
    {
        var current = "# Changelog\n\n## [Unreleased]\n\n- Old\n\n## [1.0.0] - 2026-01-01\n\n- First\n";

        var result = ChangelogNotes.ReplaceUnreleased(current, Block);

        Assert.AreEqual("# Changelog\n\n## [Unreleased]\n\n### Added\n- New thing\n\n## [1.0.0] - 2026-01-01\n\n- First\n", result);
    }

    [TestMethod]
    public void Inserts_before_first_release()
    {
        var result = ChangelogNotes.ReplaceUnreleased("# Changelog\n\n## [1.0.0]\n\n- First\n", Block);

        Assert.AreEqual("# Changelog\n\n## [Unreleased]\n\n### Added\n- New thing\n\n## [1.0.0]\n\n- First\n", result);
    }

    [TestMethod]
    public void Creates_missing_file()
    {
        Assert.AreEqual("# Changelog\n\n## [Unreleased]\n\n### Added\n- New thing\n", ChangelogNotes.ReplaceUnreleased(null, Block));
    }

    [TestMethod]
    public void Reads_version_section()
    {
        var section = ChangelogNotes.Section("# Changelog\n\n## [2.0.0] - x\n\n- Two\n\n## [1.0.0]\n\n- One\n", "2.0.0");

        Assert.AreEqual("- Two", section);
    }

    [TestMethod]
    public void Missing_section_is_null()
    {
        Assert.IsNull(ChangelogNotes.Section("# Changelog\n\n## [1.0.0]\n\n- One\n", "2.0.0"));
    }

    [TestMethod]
    public void Includes_project_folders()
    {
        CollectionAssert.AreEqual(new[] { "source/DevTools.Daemon/**" }, ChangelogNotes.IncludePaths(Product.Daemon).ToArray());
    }
}
