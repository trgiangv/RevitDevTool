using DevTools.Testing.Abstractions.Runtime;

namespace DevTools.Testing.Abstractions.Tests;

[TestClass]
public sealed class InternalMembersTests
{
    [TestMethod]
    public void Resolves_non_public_members_by_kind()
    {
        Assert.AreEqual("_secret", InternalMembers.Field(typeof(Probe), "_secret").Name);
        Assert.AreEqual("Hidden", InternalMembers.Property(typeof(Probe), "Hidden").Name);
        Assert.AreEqual("Add", InternalMembers.Method(typeof(Probe), "Add", typeof(int), typeof(int)).Name);
        Assert.AreEqual(
            3,
            InternalMembers.Method(typeof(Probe), "Add", typeof(int), typeof(int)).Invoke(new Probe(), [1, 2]));
        Assert.AreEqual(2, InternalMembers.Constructor(typeof(Probe), typeof(int)).GetParameters().Length + 1);
        Assert.AreEqual("Shared", InternalMembers.Field(typeof(Probe), "Shared", isStatic: true).Name);
    }

    [TestMethod]
    public void Lookups_are_cached()
    {
        Assert.AreSame(
            InternalMembers.Field(typeof(Probe), "_secret"),
            InternalMembers.Field(typeof(Probe), "_secret"));
        Assert.AreSame(
            InternalMembers.Method(typeof(Probe), "Add", typeof(int), typeof(int)),
            InternalMembers.Method(typeof(Probe), "Add", typeof(int), typeof(int)));
    }

    [TestMethod]
    public void Instance_and_static_lookups_do_not_collide()
    {
        Assert.IsNull(InternalMembers.TryField(typeof(Probe), "Shared"));
        Assert.IsNull(InternalMembers.TryField(typeof(Probe), "_secret", isStatic: true));
    }

    [TestMethod]
    public void A_miss_names_the_member_and_the_assembly_version()
    {
        var field = Assert.ThrowsExactly<MissingMemberException>(
            () => InternalMembers.Field(typeof(Probe), "_gone"));
        StringAssert.Contains(field.Message, "Probe._gone");
        StringAssert.Contains(field.Message, typeof(Probe).Assembly.GetName().Version!.ToString());

        var type = Assert.ThrowsExactly<MissingMemberException>(
            () => InternalMembers.Type(typeof(Probe).Assembly, "No.Such.Type"));
        StringAssert.Contains(type.Message, "No.Such.Type");

        Assert.ThrowsExactly<MissingMemberException>(
            () => InternalMembers.Method(typeof(Probe), "Add", typeof(string)));
    }

    [TestMethod]
    public void Try_lookups_return_null_instead_of_throwing()
    {
        Assert.IsNull(InternalMembers.TryField(typeof(Probe), "_gone"));
        Assert.IsNull(InternalMembers.TryProperty(typeof(Probe), "Gone"));
    }

    [TestMethod]
    public void Type_resolves_by_full_name()
    {
        Assert.AreEqual(typeof(Probe), InternalMembers.Type(typeof(Probe).Assembly, typeof(Probe).FullName!));
    }

    private sealed class Probe
    {
        public static readonly int Shared = 1;

        private readonly int _secret = 42;

        public Probe()
        {
        }

        private Probe(int seed) => _secret = seed;

        private string Hidden => _secret.ToString();

        private int Add(int left, int right) => left + right;
    }
}
