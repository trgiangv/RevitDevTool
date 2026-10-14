using System.Text.Json;
using DevTools.Daemon.Auth;
using DevTools.Ipc;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace DevTools.Daemon.Control;

public sealed class ControlPipeHandler(
    IAuthService authService,
    ILogger<ControlPipeHandler> logger)
{
    public async Task HandleRequestAsync(string requestLine, CancellationToken ct)
    {
        ControlCommand? command;
        try
        {
            command = JsonSerializer.Deserialize(requestLine, ControlJsonContext.Default.ControlCommand);
        }
        catch (JsonException ex)
        {
            logger.ZLogWarning(ex, $"Daemon control pipe command was not JSON");
            return;
        }

        if (command?.Method == IpcConstants.Methods.SignIn)
            await SignInAsync(ct).ConfigureAwait(false);
    }

    private async Task SignInAsync(CancellationToken ct)
    {
        var result = await authService.SignInAsync(ct).ConfigureAwait(false);
        if (!result.Success)
            logger.ZLogWarning($"Daemon control pipe sign-in failed: {result.Error}");
    }
}
