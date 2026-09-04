using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DevTools.Daemon.Auth;
using DevTools.Daemon.Control;
using DevTools.Daemon.Desktop;
using DevTools.Daemon.Tools;
using DevTools.Daemon.Gateway;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using DevTools.Daemon.Tests.Support;

namespace DevTools.Daemon.Tests;

public sealed class AuthComponentTests
{
    [Fact]
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

        Assert.Equal("access", loaded?.AccessToken);
        Assert.Equal("user", loaded?.UserId);
    }

    [Fact]
    public void TokenStore_SaveLoadDelete_RoundTripsEncryptedPayload()
    {
        var path = Path.Combine(Path.GetTempPath(), $"daemon-auth-{Guid.NewGuid():N}.dat");
        var logger = NullLoggerFactory.Instance.CreateLogger("TokenStore");
        var store = new TokenStore(path, logger);

        try
        {
            Assert.Null(store.TryLoad());

            var token = new TokenData
            {
                AccessToken = "access",
                RefreshToken = "refresh",
                ExpiresAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            };
            store.Save(token);

            var loaded = store.TryLoad();
            Assert.Equal("access", loaded?.AccessToken);
            Assert.Equal("refresh", loaded?.RefreshToken);

            store.Delete();
            Assert.Null(store.TryLoad());
            Assert.False(File.Exists(path));
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void TokenStore_TryLoad_ReturnsNullForCorruptFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"daemon-auth-{Guid.NewGuid():N}.dat");
        var logger = NullLoggerFactory.Instance.CreateLogger("TokenStore");
        File.WriteAllBytes(path, Encoding.UTF8.GetBytes("not-protected"));

        try
        {
            var store = new TokenStore(path, logger);
            Assert.Null(store.TryLoad());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task AuthService_SignIn_ReturnsErrorWhenNotConfigured()
    {
        using var service = new AuthService(
            Options.Create(new AuthOptions()),
            NullLogger<AuthService>.Instance);

        var result = await service.SignInAsync(TestContext.Current.CancellationToken);
        Assert.False(result.Success);
        Assert.Contains("not configured", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AuthService_SignOut_ClearsUnauthenticatedState()
    {
        using var service = new AuthService(
            Options.Create(new AuthOptions()),
            NullLogger<AuthService>.Instance);

        await service.SignOutAsync();
        Assert.False(service.IsAuthenticated);
    }

    [Fact]
    public async Task AuthService_Refresh_ReturnsFalseWhenNotConfigured()
    {
        using var service = new AuthService(
            Options.Create(new AuthOptions()),
            NullLogger<AuthService>.Instance);

        Assert.False(await service.RefreshAsync());
    }

    [Fact]
    public async Task MachineLister_ReturnsErrorWhenNotAuthenticated()
    {
        var auth = DaemonTestDoubles.CreateAuthService(authenticated: false);
        var lister = new MachineLister(auth.Object, Options.Create(new GatewayOptions()));
        var result = await lister.ListAsync(TestContext.Current.CancellationToken);
        var text = Assert.IsType<ModelContextProtocol.Protocol.TextContentBlock>(result.Content[0]).Text;
        Assert.Contains("Not authenticated", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MachineLister_ReturnsErrorWhenGatewayUnreachable()
    {
        var auth = DaemonTestDoubles.CreateAuthService(authenticated: true, accessToken: "token");
        var lister = new MachineLister(
            auth.Object,
            Options.Create(new GatewayOptions { Url = "wss://127.0.0.1:9/tunnel" }));

        var result = await lister.ListAsync(TestContext.Current.CancellationToken);
        var text = Assert.IsType<ModelContextProtocol.Protocol.TextContentBlock>(result.Content[0]).Text;
        Assert.Contains("Failed to list machines", text, StringComparison.Ordinal);
    }
}
