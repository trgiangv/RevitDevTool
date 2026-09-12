namespace DevTools.NUnit.Runtime.Tests;

public sealed class NUnitCollapsedSelectionTests
{
    [Fact]
    public void Matches_expanded_fixture_arguments_on_the_declaring_type()
    {
        Assert.True(NUnitCollapsedSelection.Matches(
            "Ns.Fixture.Method",
            "Ns.Fixture(\"a\").Method",
            "Ns.Fixture(\"a\").Method",
            null));
    }

    [Fact]
    public void Matches_does_not_cross_nested_parameterized_suites()
    {
        Assert.False(NUnitCollapsedSelection.Matches(
            "Ns.Fixture.Method",
            "Ns.Fixture(\"a\").Inner(\"b\").Method",
            "Ns.Fixture(\"a\").Inner(\"b\").Method",
            null));
    }

    [Fact]
    public void Matches_nunit4_double_suffix_to_host_without_suffix()
    {
        const string testhost =
            "Ns.Box.Bottom_corners_share_min_z(-12.3d,45.6d,-7.8d,34.5d,67.8d,12.3d)";
        const string host =
            "Ns.Box.Bottom_corners_share_min_z(-12.3,45.6,-7.8,34.5,67.8,12.3)";

        Assert.True(NUnitCollapsedSelection.Matches(testhost, host, host, null));
        Assert.True(NUnitCollapsedSelection.Matches(
            "Ns.Box.Bottom_corners_share_min_z",
            testhost,
            testhost,
            null));
        Assert.False(NUnitCollapsedSelection.Matches(
            testhost,
            "Ns.Box.Bottom_corners_share_min_z(-99.9,45.6,-7.8,34.5,67.8,12.3)",
            "Ns.Box.Bottom_corners_share_min_z(-99.9,45.6,-7.8,34.5,67.8,12.3)",
            null));
    }

    [Fact]
    public void ToFilterXml_adds_unsuffixed_double_args_as_exact_test()
    {
        const string testhost =
            "Ns.Box.Bottom_corners_share_min_z(-12.3d,45.6d,-7.8d,34.5d,67.8d,12.3d)";
        const string host =
            "Ns.Box.Bottom_corners_share_min_z(-12.3,45.6,-7.8,34.5,67.8,12.3)";
        var xml = NUnitCollapsedSelection.ToFilterXml([testhost]);

        Assert.Contains($"<test>{testhost}</test>", xml, StringComparison.Ordinal);
        Assert.Contains($"<test>{host}</test>", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void Matches_rider_empty_parens_group_to_parameterized_leaves()
    {
        const string host =
            "Ns.Box.Bottom_corners_share_min_z(-12.3,45.6,-7.8,34.5,67.8,12.3)";

        Assert.True(NUnitCollapsedSelection.Matches(
            "Ns.Box.Bottom_corners_share_min_z()",
            host,
            host,
            null));
        Assert.True(NUnitCollapsedSelection.IsDottedLeafWithoutArgs(
            "Ns.Box.Bottom_corners_share_min_z()"));
    }

    [Fact]
    public void SameFullName_ignores_spaces_after_commas_in_args()
    {
        Assert.True(NUnitNameSyntax.Same(
            "Ns.Box.Method(-12.3d, 45.6d, 12.3d)",
            "Ns.Box.Method(-12.3,45.6,12.3)"));
    }

    [Fact]
    public void ToFilterXml_empty_parens_group_emits_method_filter()
    {
        var xml = NUnitCollapsedSelection.ToFilterXml(
            ["Ns.Box.Bottom_corners_share_min_z()"]);

        Assert.Contains("<method>Bottom_corners_share_min_z</method>", xml, StringComparison.Ordinal);
        Assert.Contains("re=\"1\"", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void ToFilterXml_stripped_double_args_still_regex_matches_host_with_suffix()
    {
        const string stripped =
            "Ns.Box.Bottom_corners_share_min_z(-12.3,45.6,-7.8,34.5,67.8,12.3)";
        var xml = NUnitCollapsedSelection.ToFilterXml([stripped]);

        Assert.Contains("re=\"1\"", xml, StringComparison.Ordinal);
        Assert.Contains("[dDfFmML]?", xml, StringComparison.Ordinal);
        Assert.DoesNotContain("<method>Bottom_corners_share_min_z</method>", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void StripNumericTypeSuffixes_leaves_quoted_names()
    {
        Assert.Equal(
            "Ns.Box.Method(\"Wide_box\")",
            NUnitNameSyntax.StripNumericTypeSuffixes(
                "Ns.Box.Method(\"Wide_box\")"));
    }
}
