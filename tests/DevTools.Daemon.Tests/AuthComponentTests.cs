using System.Text;
using System.Text.Json;
using DevTools.Daemon.Auth;
using DevTools.Daemon.Control;
using DevTools.Daemon.Gateway;
using DevTools.Daemon.Mcp.Tools;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using DevTools.Daemon.Tests.Support;

namespace DevTools.Daemon.Tests;

[TestClass]
public sealed class AuthComponentTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void TokenData_RoundTripsThroughJson()
    {
        var token = new TokenData
        {
            AccessToken = "access",
            RefreshToken = "refresh",
            ExpiresAt = 123,
            UserId = "user",
            Email = "a@example.com",
            DisplayName = "Name",
            AvatarUrl = "https://avatar",
        };

        var json = JsonSerializer.Serialize(token, ControlJsonContext.Default.TokenData);
        var loaded = JsonSerializer.Deserialize(json, ControlJsonContext.Default.TokenData);

        Assert.AreEqual("access", loaded?.AccessToken);
        Assert.AreEqual("user", loaded?.UserId);
    }

    [TestMethod]
    public void TokenStore_SaveLoadDelete_RoundTripsEncryptedPayload()
    {
        var path = Path.Combine(Path.GetTempPath(), $"daemon-auth-{Guid.NewGuid():N}.dat");
        var logger = NullLoggerFactory.Instance.CreateLogger("TokenStore");
        var store = new TokenStore(path, logger);

        try
        {
            Assert.IsNull(store.TryLoad());

            var token = new TokenData
            {
                AccessToken = "access",
                RefreshToken = "refresh",
                ExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            };
            store.Save(token);

            var loaded = store.TryLoad();
            Assert.AreEqual("access", loaded?.AccessToken);
            Assert.AreEqual("refresh", loaded?.RefreshToken);

            store.Delete();
            Assert.IsNull(store.TryLoad());
            Assert.IsFalse(File.Exists(path));
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [TestMethod]
    public void TokenStore_TryLoad_ReturnsNullForCorruptFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"daemon-auth-{Guid.NewGuid():N}.dat");
        var logger = NullLoggerFactory.Instance.CreateLogger("TokenStore");
        File.WriteAllBytes(path, Encoding.UTF8.GetBytes("not-protected"));

        try
        {
            var store = new TokenStore(path, logger);
            Assert.IsNull(store.TryLoad());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task AuthService_SignIn_ReturnsErrorWhenNotConfigured()
    {
        using var service = new AuthService(
            Options.Create(new AuthOptions()),
            NullLogger<AuthService>.Instance);

        var result = await service.SignInAsync(TestContext.CancellationToken);
        Assert.IsFalse(result.Success);
        Assert.Contains("not configured", result.Error!, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public async Task AuthService_SignOut_ClearsUnauthenticatedState()
    {
        using var service = new AuthService(
            Options.Create(new AuthOptions()),
            NullLogger<AuthService>.Instance);

        await service.SignOutAsync();
        Assert.IsFalse(service.IsAuthenticated);
    }

    [TestMethod]
    public async Task AuthService_Refresh_ReturnsFalseWhenNotConfigured()
    {
        using var service = new AuthService(
            Options.Create(new AuthOptions()),
            NullLogger<AuthService>.Instance);

        Assert.IsFalse(await service.RefreshAsync());
    }

    [TestMethod]
    public async Task ListMachinesTool_ReturnsErrorWhenNotAuthenticated()
    {
        var auth = DaemonTestDoubles.CreateAuthService(authenticated: false);
        var tool = new ListMachinesTool(auth.Object, Options.Create(new GatewayOptions()));
        var result = await tool.ListAsync(TestContext.CancellationToken);
        var text = Assert.IsInstanceOfType<ModelContextProtocol.Protocol.TextContentBlock>(result.Content[0]).Text;
        Assert.Contains("Not authenticated", text, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task ListMachinesTool_ReturnsErrorWhenGatewayUnreachable()
    {
        var auth = DaemonTestDoubles.CreateAuthService(authenticated: true, accessToken: "token");
        var tool = new ListMachinesTool(
            auth.Object,
            Options.Create(new GatewayOptions { Url = "wss://127.0.0.1:9/tunnel" }));

        var result = await tool.ListAsync(TestContext.CancellationToken);
        var text = Assert.IsInstanceOfType<ModelContextProtocol.Protocol.TextContentBlock>(result.Content[0]).Text;
        Assert.Contains("Failed to list machines", text, StringComparison.Ordinal);
    }
}
