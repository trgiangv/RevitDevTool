using DevTools.ModelsContext.Engine;
using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Tests.Shared;

namespace DevTools.ModelsContext.Tests;

[TestClass]
public sealed class TypeBoundTests
{
    private static readonly FakeTypeBound Model = FakeTypeBound.Create(FakeCatalogs.Seed);

    [TestMethod]
    public void HasValueMissesTypeParameters()
    {
        var indexes = Enumerable.Range(0, Model.Elements.Length).ToList();

        var typed = indexes.Where(index => Model.ValueOf(index) is { Length: > 0 }).ToList();

        Assert.IsNotEmpty(typed);
        Assert.IsFalse(typed.Any(Model.HasValueRule));
        Assert.IsTrue(typed.Any(Model.HasNoValueRule));
    }

    [TestMethod]
    public void PresenceMatchesTypeValues()
    {
        foreach (var op in new[] { FilterOperator.Defined, FilterOperator.NotEmpty, FilterOperator.Empty })
        {
            var expected = Expected(op, index => true);

            Assert.AreEqual(expected, Presence(op), op.ToString());
        }
    }

    [TestMethod]
    public void UndefinedMatchesMissingTypes()
    {
        var expected = Enumerable.Range(0, Model.Elements.Length).Count(index => Model.ValueOf(index) is null);
        var missing = Model.Members.Split(Model.Presence.TypesFor(FilterOperator.Defined)).Outside;

        Assert.AreEqual(expected, Enumerable.Range(0, Model.Elements.Length)
            .Count(index => !Model.HasValueRule(index) && !Model.HasNoValueRule(index)
                            && missing.Contains(Model.Elements[index])));
    }

    [TestMethod]
    public void GuardDropsNotEqualMisses()
    {
        var defined = Defined();
        var all = Enumerable.Range(0, Model.Elements.Length).ToList();
        var raw = all.Count(index => Model.NativeNotEquals(index, "2 HR"));
        var guarded = all.Count(index => defined.Contains(index) && Model.NativeNotEquals(index, "2 HR"));
        var expected = all.Count(index => Model.ValueOf(index) is { } value && value != "2 HR");

        Assert.AreEqual(expected, guarded);
        Assert.IsGreaterThan(guarded, raw);
    }

    [TestMethod]
    public void EqualsEmptyNeedsGuard()
    {
        var defined = Defined();
        var all = Enumerable.Range(0, Model.Elements.Length).ToList();
        var guarded = all.Count(index => defined.Contains(index) && Model.NativeEquals(index, ""));

        Assert.AreEqual(all.Count(index => Model.ValueOf(index) is ""), guarded);
    }

    [TestMethod]
    public void SplitKeepsCandidateOrder()
    {
        var members = new TypeMembership([10, 11, 12, 13], [1, 2, 1, -1]);

        var split = members.Split([1]);

        CollectionAssert.AreEqual(new long[] { 10, 12 }, split.Inside.ToList());
        CollectionAssert.AreEqual(new long[] { 11, 13 }, split.Outside.ToList());
    }

    [TestMethod]
    public void SplitIdsKeepsCandidateOrder()
    {
        var members = new TypeMembership([10, 11, 12, 13], [1, 2, 1, -1]);

        var split = members.SplitIds([13, 11, 99]);

        CollectionAssert.AreEqual(new long[] { 11, 13 }, split.Inside.ToList());
        CollectionAssert.AreEqual(new long[] { 10, 12 }, split.Outside.ToList());
    }

    [TestMethod]
    public void SplitRejectsMismatchedArrays()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new TypeMembership([1, 2], [1]));
    }

    [TestMethod]
    public void TypesForPicksOperatorSet()
    {
        var presence = new TypePresence([1, 2], [3]);

        CollectionAssert.AreEquivalent(new long[] { 1, 2 }, presence.TypesFor(FilterOperator.NotEmpty).ToList());
        CollectionAssert.AreEquivalent(new long[] { 3 }, presence.TypesFor(FilterOperator.Empty).ToList());
        CollectionAssert.AreEquivalent(new long[] { 1, 2, 3 }, presence.TypesFor(FilterOperator.Defined).ToList());
        Assert.IsTrue(TypePresence.None.IsEmpty);
        Assert.IsFalse(presence.IsEmpty);
    }

    private static int Expected(FilterOperator op, Func<int, bool> include)
    {
        return Enumerable.Range(0, Model.Elements.Length).Count(index => include(index) && Model.ValueOf(index) is { } value
            && op switch
            {
                FilterOperator.NotEmpty => value.Length > 0,
                FilterOperator.Empty => value.Length == 0,
                _ => true
            });
    }

    private static HashSet<int> Defined()
    {
        var types = Model.Members.Split(Model.Presence.TypesFor(FilterOperator.Defined)).Inside.ToHashSet();
        return Enumerable.Range(0, Model.Elements.Length)
            .Where(index => Model.HasValueRule(index) || Model.HasNoValueRule(index) || types.Contains(Model.Elements[index]))
            .ToHashSet();
    }

    private static int Presence(FilterOperator op)
    {
        var withValue = Model.Members.Split(Model.Presence.TypesFor(FilterOperator.NotEmpty)).Inside.ToHashSet();
        var withoutValue = Model.Members.Split(Model.Presence.TypesFor(FilterOperator.Empty)).Inside.ToHashSet();
        return Enumerable.Range(0, Model.Elements.Length).Count(index =>
        {
            var id = Model.Elements[index];
            var hasValue = Model.HasValueRule(index);
            var hasNoValue = Model.HasNoValueRule(index);
            return op switch
            {
                FilterOperator.NotEmpty => hasValue || withValue.Contains(id),
                FilterOperator.Empty => (hasNoValue && !withValue.Contains(id)) || (!hasValue && withoutValue.Contains(id)),
                _ => hasValue || hasNoValue || withValue.Contains(id) || withoutValue.Contains(id)
            };
        });
    }
}
