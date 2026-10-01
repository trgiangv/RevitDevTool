using DevTools.Testing.Abstractions.Contracts;

namespace DevTools.Testing.Abstractions.Tests;

[TestClass]
public sealed class TestSelectionMatcherTests
{
    private static readonly TestDiscoveredTest Test = new(
        "uid-1",
        "Adds(1, 2)",
        FullName: "Ns.Calc.Adds",
        MethodName: "Adds");

    [TestMethod]
    public void All_matches_everything()
    {
        var matcher = TestSelectionMatcher.For(TestSelection.All);

        Assert.IsTrue(matcher.Matches(Test));
        Assert.IsFalse(matcher.SelectsNothing);
    }

    [TestMethod]
    public void Ids_match_by_uid_or_full_name_and_ignore_blank_entries()
    {
        var matcher = TestSelectionMatcher.For(TestSelection.FromTestIds([" uid-1 ", " "]));

        Assert.IsTrue(matcher.Matches(Test));
        Assert.IsTrue(TestSelectionMatcher.For(TestSelection.FromTestIds(["Ns.Calc.Adds"])).Matches(Test));
        Assert.IsFalse(TestSelectionMatcher.For(TestSelection.FromTestIds(["uid-2"])).Matches(Test));
    }

    [TestMethod]
    public void Empty_ids_select_nothing_not_everything()
    {
        var matcher = TestSelectionMatcher.For(TestSelection.FromTestIds([]));

        Assert.IsTrue(matcher.SelectsNothing);
        Assert.IsFalse(matcher.Matches(Test));
    }

    [TestMethod]
    public void Names_match_exactly_or_by_case_insensitive_substring()
    {
        Assert.IsTrue(TestSelectionMatcher.For(TestSelection.FromNames(["Adds(1, 2)"])).Matches(Test));
        Assert.IsTrue(TestSelectionMatcher.For(TestSelection.FromNames(["calc.ADDS"])).Matches(Test));
        Assert.IsTrue(TestSelectionMatcher.For(TestSelection.FromNames(["adds"])).Matches(Test));
        Assert.IsFalse(TestSelectionMatcher.For(TestSelection.FromNames(["Subtracts"])).Matches(Test));
    }

    [TestMethod]
    public void Names_are_not_matched_against_ids_and_ids_not_against_names()
    {
        Assert.IsFalse(TestSelectionMatcher.For(TestSelection.FromNames(["x"])).Matches(
            Test,
            alternateId: () => "x"));
        Assert.IsFalse(TestSelectionMatcher.For(TestSelection.FromTestIds(["Adds"])).Matches(Test));
    }

    [TestMethod]
    public void Method_name_override_and_alternate_id_extend_the_match()
    {
        Assert.IsTrue(TestSelectionMatcher.For(TestSelection.FromNames(["Renamed"]))
            .Matches(Test, methodName: "Renamed"));
        Assert.IsTrue(TestSelectionMatcher.For(TestSelection.FromTestIds(["deferred-1"]))
            .Matches(Test, alternateId: () => "deferred-1"));
    }

    [TestMethod]
    public void Alternate_id_is_only_computed_when_ids_are_matched()
    {
        var calls = 0;
        string? Alternate()
        {
            calls++;
            return null;
        }

        TestSelectionMatcher.For(TestSelection.All).Matches(Test, alternateId: Alternate);
        TestSelectionMatcher.For(TestSelection.FromNames(["nope"])).Matches(Test, alternateId: Alternate);
        Assert.AreEqual(0, calls);

        TestSelectionMatcher.For(TestSelection.FromTestIds(["nope"])).Matches(Test, alternateId: Alternate);
        Assert.AreEqual(1, calls);
    }

    [TestMethod]
    public void Framework_filter_selects_nothing()
    {
        var matcher = TestSelectionMatcher.For(TestSelection.FromFrameworkFilter("x", "y"));

        Assert.IsTrue(matcher.SelectsNothing);
        Assert.IsFalse(matcher.Matches(Test));
    }
}
