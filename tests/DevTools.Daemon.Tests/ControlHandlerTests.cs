using DevTools.Daemon.Auth;
using DevTools.Daemon.Control;
using DevTools.Daemon.Tests.Support;
using DevTools.Ipc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.IO.Pipes;
using System.Text;

namespace DevTools.Daemon.Tests;

[DoNotParallelize]
[TestClass]
public sealed class ControlHandlerTests : WpfApplicationTestBase
{
    [TestMethod]
    public async Task HandleRequestAsync_SignIn_StartsDaemonSignInWithoutWaiting()
    {
        var signInStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSignIn = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var auth = DaemonTestDoubles.CreateAuthService();
        auth.Setup(a => a.SignInAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken _) =>
            {
                signInStarted.TrySetResult();
                await releaseSignIn.Task.ConfigureAwait(false);
                return new AuthResult(true);
            });

        var handler = new ControlHandler(auth.Object, NullLogger<ControlHandler>.Instance);
        try
        {
            await handler.HandleRequestAsync(
                """{"method":"control/sign_in"}""",
                CancellationToken.None);

            await signInStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
            auth.Verify(a => a.SignInAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            releaseSignIn.TrySetResult();
        }
    }

    [TestMethod]
    public async Task HandleRequestAsync_UnknownOrInvalid_DoesNotStartSignIn()
    {
        var auth = DaemonTestDoubles.CreateAuthService();
        var handler = CreateHandler(auth);
        await handler.HandleRequestAsync(
            """{"method":"control/status"}""",
            CancellationToken.None);
        await handler.HandleRequestAsync("{", CancellationToken.None);

        auth.Verify(a => a.SignInAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task ControlPipe_SignInDoesNotBlockTheNextCommand()
    {
        var signInStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseSignIn = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var auth = DaemonTestDoubles.CreateAuthService();
        auth.Setup(a => a.SignInAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken _) =>
            {
                signInStarted.TrySetResult();
                await releaseSignIn.Task.ConfigureAwait(false);
                return new AuthResult(true);
            });

        var handler = new ControlHandler(auth.Object, NullLogger<ControlHandler>.Instance);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        var pipeName = $"{IpcConstants.ControlPipeName}_{Guid.NewGuid():N}";
        var service = new ControlHostedService(handler, NullLogger<ControlHostedService>.Instance)
        {
            PipeName = pipeName,
        };
        await service.StartAsync(cts.Token);

        try
        {
            await SendAsync(pipeName, IpcConstants.Methods.SignIn, cts.Token);
            await signInStarted.Task.WaitAsync(TimeSpan.FromSeconds(3), cts.Token);
            await SendAsync(pipeName, "control/status", cts.Token);
            auth.Verify(a => a.SignInAsync(It.IsAny<CancellationToken>()), Times.Once);
        }
        finally
        {
            releaseSignIn.TrySetResult();
            await cts.CancelAsync();
            await service.StopAsync(CancellationToken.None);
        }
    }

    private static async Task SendAsync(string pipeName, string method, CancellationToken ct)
    {
        await using var client = new NamedPipeClientStream(
            ".",
            pipeName,
            PipeDirection.InOut,
            PipeOptions.Asynchronous);
        await client.ConnectAsync(2000, ct);
        var request = Encoding.UTF8.GetBytes($"{{\"method\":\"{method}\"}}\n");
        await client.WriteAsync(request, ct);
        await client.FlushAsync(ct);
    }

    private static ControlHandler CreateHandler(Mock<IAuthService> auth) =>
        new(auth.Object, NullLogger<ControlHandler>.Instance);

}
