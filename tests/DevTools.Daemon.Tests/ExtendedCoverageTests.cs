using DevTools.Daemon.Auth;
using DevTools.Daemon.Control;
using DevTools.Utilities;
using Duende.IdentityModel.OidcClient.Browser;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace DevTools.Daemon.Tests;

[TestClass]
public sealed class ExtendedCoverageTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task AuthBrowser_CallbackSuccess_ReturnsResponseUrl()
    {
        var port = GetFreePort();
        var callback = $"http://127.0.0.1:{port}/callback";
        var browser = new AuthBrowser(new AuthOptions { LoopbackPort = port }, static _ => { });
        var invoke = Task.Run(() => browser.InvokeAsync(
            new BrowserOptions("about:blank", callback),
            TestContext.CancellationToken));

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        for (var attempt = 0; attempt < 80; attempt++)
        {
            try
            {
                await client.GetAsync($"{callback}?code=abc", TestContext.CancellationToken);
                break;
            }
            catch
            {
                await Task.Delay(50, TestContext.CancellationToken);
            }
        }

        var result = await invoke.WaitAsync(TimeSpan.FromSeconds(10), TestContext.CancellationToken);
        Assert.AreEqual(BrowserResultType.Success, result.ResultType);
        Assert.Contains("code=abc", result.Response, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task AuthBrowser_CallbackDenied_ReturnsDeniedResponse()
    {
        var port = GetFreePort();
        var callback = $"http://127.0.0.1:{port}/callback";
        var browser = new AuthBrowser(new AuthOptions { LoopbackPort = port }, static _ => { });
        var invoke = Task.Run(() => browser.InvokeAsync(
            new BrowserOptions("about:blank", callback),
            TestContext.CancellationToken));

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        for (var attempt = 0; attempt < 80; attempt++)
        {
            try
            {
                await client.GetAsync($"{callback}?error=access_denied", TestContext.CancellationToken);
                break;
            }
            catch
            {
                await Task.Delay(50, TestContext.CancellationToken);
            }
        }

        var result = await invoke.WaitAsync(TimeSpan.FromSeconds(10), TestContext.CancellationToken);
        Assert.AreEqual(BrowserResultType.Success, result.ResultType);
        Assert.Contains("error=access_denied", result.Response, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task AuthService_WithStoredToken_RefreshAndSignOut()
    {
        var tokenPath = Path.Combine(AppUtils.GetApplicationDataPath(), "auth.dat");
        var backup = File.Exists(tokenPath) ? File.ReadAllBytes(tokenPath) : null;
        try
        {
            var token = new TokenData
            {
                AccessToken = "access-token",
                RefreshToken = "refresh-token",
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-10).ToUnixTimeSeconds(),
                UserId = "user",
                Email = "user@example.com",
            };
            var json = JsonSerializer.Serialize(token, ControlJsonContext.Default.TokenData);
            File.WriteAllBytes(
                tokenPath,
                ProtectedData.Protect(Encoding.UTF8.GetBytes(json), null, DataProtectionScope.CurrentUser));

            using var service = new AuthService(
                Options.Create(new AuthOptions { Issuer = "https://example.com", ClientId = "client" }),
                NullLogger<AuthService>.Instance);

            Assert.IsTrue(service.IsAuthenticated);
            Assert.AreEqual("user@example.com", service.Email);
            Assert.IsFalse(await service.RefreshAsync());
            await service.SignOutAsync();
            Assert.IsFalse(service.IsAuthenticated);
        }
        finally
        {
            if (backup is null)
                File.Delete(tokenPath);
            else
                File.WriteAllBytes(tokenPath, backup);
        }
    }

    [TestMethod]
    public async Task Program_Main_StdioArg_ReturnsZeroWhenStdinCloses()
    {
        var originalIn = Console.In;
        try
        {
            var input = new MemoryStream();
            Console.SetIn(new StreamReader(input));
            var task = Task.Run(() => Program.Main(["--stdio"]));
            input.Close();
            Assert.AreEqual(0, await task);
        }
        finally
        {
            Console.SetIn(originalIn);
        }
    }

    private static int GetFreePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }
}
