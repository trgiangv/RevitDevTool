using System.Security.Principal;

namespace DevTools.Telemetry.Tests;

[TestClass]
public sealed class UserIdentityTests
{
    [TestMethod]
    public void Get_returns_stable_32_hex_id_without_sid()
    {
        var first = UserIdentity.Get();
        var second = UserIdentity.Get();

        Assert.AreEqual(first, second);
        Assert.MatchesRegex("^[0-9a-f]{32}$", first);
        using var identity = WindowsIdentity.GetCurrent();
        Assert.DoesNotContain(identity.User!.Value, first, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void Hash_is_deterministic_and_input_sensitive()
    {
        Assert.AreEqual(UserIdentity.Hash("machine|S-1-5-21-1"), UserIdentity.Hash("machine|S-1-5-21-1"));
        Assert.AreNotEqual(UserIdentity.Hash("machine|S-1-5-21-1"), UserIdentity.Hash("machine|S-1-5-21-2"));
    }
}
