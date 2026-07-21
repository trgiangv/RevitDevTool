using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace RevitDevTool.Server.Tests;

public sealed class CurrentUserPipeFactoryTests
{
    [Fact]
    public void CreateDuplexServer_GrantsFullControlToCurrentUser()
    {
        var pipeName = $"DevTools_Test_{Guid.NewGuid():N}";
        using var server = CurrentUserPipeFactory.CreateDuplexServer(pipeName, maxInstances: 1);

        var security = server.GetAccessControl();
        var currentUserSid = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("Current Windows user has no SID.");

        var allowRules = security
            .GetAccessRules(includeExplicit: true, includeInherited: false, typeof(SecurityIdentifier))
            .Cast<PipeAccessRule>()
            .Where(rule => rule.AccessControlType == AccessControlType.Allow);

        Assert.Contains(
            allowRules,
            rule => rule.IdentityReference.Equals(currentUserSid)
                    && (rule.PipeAccessRights & PipeAccessRights.FullControl) == PipeAccessRights.FullControl);
    }
}
