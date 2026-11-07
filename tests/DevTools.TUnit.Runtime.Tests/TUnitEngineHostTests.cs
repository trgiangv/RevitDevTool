using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Services;
using TUnit.Engine.Exceptions;

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

    private const string FrameworkMember = "Method not found: 'TUnit.Core.TestContext TUnit.Core.Services.IContextProvider.CreateTestContext()'.";

    [TestMethod]
    public void Framework_member_error_is_a_bind_failure()
    {
        var original = new MissingMethodException(FrameworkMember);

        Assert.AreSame(original, TUnitEngineBindFailure.TryRead(ErrorNode(original)));
    }

    [TestMethod]
    public void Wrapped_error_returns_the_original()
    {
        var original = new TypeLoadException("Could not load type 'X' from assembly 'Microsoft.Testing.Platform'.");

        Assert.AreSame(original, TUnitEngineBindFailure.TryRead(ErrorNode(new TestFailedException(new TestFailedException(original)))));
    }

    [TestMethod]
    public void Other_errors_stay_results()
    {
        Assert.IsNull(TUnitEngineBindFailure.TryRead(ErrorNode(new InvalidOperationException("TUnit.Core failed."))));
        Assert.IsNull(TUnitEngineBindFailure.TryRead(ErrorNode(new MissingMethodException("Method not found: 'UserCode.Helper.Run()'."))));
        Assert.IsNull(TUnitEngineBindFailure.TryRead(ErrorNode(new TestFailedException(new InvalidOperationException(FrameworkMember)))));
    }

    [TestMethod]
    public void Node_without_error_stays_result()
    {
        var node = new TestNode
        {
            Uid = new TestNodeUid("passed"),
            DisplayName = $"Unhandled exception - MissingMethodException: {FrameworkMember}",
            Properties = new PropertyBag(PassedTestNodeStateProperty.CachedInstance),
        };

        Assert.IsNull(TUnitEngineBindFailure.TryRead(node));
    }

    private static TestNode ErrorNode(Exception exception) =>
        new()
        {
            Uid = new TestNodeUid("unhandled"),
            DisplayName = $"Unhandled exception - {exception.GetType().Name}: {exception.Message}",
            Properties = new PropertyBag(new ErrorTestNodeStateProperty(exception)),
        };

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
