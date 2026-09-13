using DevTools.NUnit.MTP;
using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.NUnit.MTP.Tests;

[TestClass]
public sealed class NUnitIdentityIndexTests
{
    [TestMethod]
    public void Resolve_published_uid_is_a_leaf()
    {
        var leaf = Leaf(
            "Ns.Box.Bottom_corners_share_min_z(-12.3,45.6,-7.8,34.5,67.8,12.3)",
            "Bottom_corners_share_min_z(-12.3,45.6,-7.8,34.5,67.8,12.3)");
        var hit = NUnitIdentityIndex.Build([leaf]).Resolve(leaf.TestId);

        Assert.AreEqual(NUnitIdentityKind.Leaf, hit.Kind);
        Assert.AreEqual(leaf.TestId, hit.Leaves.Single().TestId);
    }

    [TestMethod]
    public void Resolve_method_and_empty_parens_are_the_group()
    {
        var one = Leaf(
            "Ns.Box.Bottom_corners_share_min_z(-12.3,45.6,-7.8,34.5,67.8,12.3)",
            "Bottom_corners_share_min_z(-12.3,45.6,-7.8,34.5,67.8,12.3)");
        var two = Leaf(
            "Ns.Box.Bottom_corners_share_min_z(-99.9,-88.8,-10.5,-10.4,-20.3,5.7)",
            "Bottom_corners_share_min_z(-99.9,-88.8,-10.5,-10.4,-20.3,5.7)");
        var index = NUnitIdentityIndex.Build([one, two]);

        Assert.AreEqual(NUnitIdentityKind.Group, index.Resolve("Ns.Box.Bottom_corners_share_min_z").Kind);
        Assert.AreEqual(NUnitIdentityKind.Group, index.Resolve("Ns.Box.Bottom_corners_share_min_z()").Kind);
        Assert.AreEqual(2, index.Select(["Ns.Box.Bottom_corners_share_min_z()"]).Count);
    }

    [TestMethod]
    public void Resolve_display_name_with_args_is_that_leaf()
    {
        var leaf = Leaf(
            "Ns.Box.Bottom_corners_share_min_z(-12.3,45.6,-7.8,34.5,67.8,12.3)",
            "Bottom_corners_share_min_z(-12.3,45.6,-7.8,34.5,67.8,12.3)");
        var hit = NUnitIdentityIndex.Build([leaf]).Resolve(leaf.DisplayName);

        Assert.AreEqual(NUnitIdentityKind.Leaf, hit.Kind);
        Assert.AreEqual(leaf.TestId, hit.Leaves.Single().TestId);
    }

    [TestMethod]
    public void Resolve_bare_method_name_is_unknown()
    {
        var leaf = new TestDiscoveredTest(
            "Ns.Box.PlainTest_Passes",
            "PlainTest_Passes",
            "Ns.Box.PlainTest_Passes",
            "Ns.Box",
            "PlainTest_Passes",
            Namespace: "Ns",
            TypeName: "Box");
        var hit = NUnitIdentityIndex.Build([leaf]).Resolve("PlainTest_Passes");

        Assert.AreEqual(NUnitIdentityKind.Unknown, hit.Kind);
        Assert.IsEmpty(hit.Leaves);
    }

    [TestMethod]
    public void Resolve_canon_strips_numeric_suffixes()
    {
        var leaf = Leaf(
            "Ns.Box.Bottom_corners_share_min_z(-12.3,45.6,-7.8,34.5,67.8,12.3)",
            "Bottom_corners_share_min_z(-12.3,45.6,-7.8,34.5,67.8,12.3)");
        var hit = NUnitIdentityIndex.Build([leaf]).Resolve(
            "Ns.Box.Bottom_corners_share_min_z(-12.3d,45.6d,-7.8d,34.5d,67.8d,12.3d)");

        Assert.AreEqual(NUnitIdentityKind.Leaf, hit.Kind);
        Assert.AreEqual(leaf.TestId, hit.Leaves.Single().TestId);
    }

    static TestDiscoveredTest Leaf(string testId, string displayName) =>
        new(
            testId,
            displayName,
            testId,
            "Ns.Box",
            "Bottom_corners_share_min_z",
            Namespace: "Ns",
            TypeName: "Box");
}
