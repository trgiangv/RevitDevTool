using Microsoft.Testing.Platform.Services;

namespace DevTools.TUnit.Runtime.Tests;

#pragma warning disable TPEXP

[TestClass]
public sealed class TUnitEngineHostTests
{
    [TestMethod]
    public void Client_info_is_mtp_2_4_non_stateful()
    {
        IClientInfo info = new TUnitEngineClientInfo();

        Assert.AreEqual("devtools-revit-host", info.Id);
        Assert.AreEqual("1.0", info.Version);
        Assert.IsFalse(info.Capabilities.IsStateful);
    }

    [TestMethod]
    public void Engine_unhandled_node_for_a_framework_member_is_a_bind_failure()
    {
        const string message = "Method not found: 'TUnit.Core.TestContext TUnit.Core.Services.IContextProvider.CreateTestContext()'.";

        var failure = TUnitEngineBindFailure.TryRead($"Unhandled exception - MissingMethodException: {message}");

        Assert.IsInstanceOfType<MissingMethodException>(failure);
        Assert.AreEqual(message, failure.Message);
    }

    [TestMethod]
    [DataRow("Unhandled exception - InvalidOperationException: TUnit.Core failed.")]
    [DataRow("Unhandled exception - MissingMethodException: Method not found: 'UserCode.Helper.Run()'.")]
    [DataRow("Some_test (Method not found: 'TUnit.Core.Gone')")]
    public void Other_nodes_stay_results(string displayName)
    {
        Assert.IsNull(TUnitEngineBindFailure.TryRead(displayName));
    }

    [TestMethod]
    public void Engine_bindings_resolve_against_the_pinned_tunit_engine()
    {
        // Fails with the missing member named when a TUnit or MTP bump moves an internal.
        var bindings = TUnitEngineBindings.Instance;

        Assert.AreSame(bindings, TUnitEngineBindings.Instance);
        var sessionUid = new Microsoft.Testing.Platform.TestHost.SessionUid("probe");
        Assert.IsNotNull(bindings.CreateSessionContext(sessionUid));
        Assert.IsNotNull(bindings.CreateCreateContext(sessionUid, CancellationToken.None));
        Assert.IsNotNull(bindings.CreateCloseContext(sessionUid, CancellationToken.None));
    }

    [TestMethod]
    public void Engine_bindings_build_a_framework_over_the_hand_made_service_set()
    {
        var bindings = TUnitEngineBindings.Instance;
        var directory = System.IO.Path.GetTempPath();

        var services = bindings.CreateServices(directory, directory);
        var framework = bindings.CreateFramework(services);

        Assert.IsNotNull(framework);
        Assert.AreEqual("TUnit.Engine.Framework.TUnitTestFramework", framework.GetType().FullName);
    }
}
