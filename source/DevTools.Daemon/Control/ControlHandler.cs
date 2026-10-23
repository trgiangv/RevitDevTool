using DevTools.Daemon.Auth;
using DevTools.Ipc;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using ZLogger;

namespace DevTools.Daemon.Control;

public sealed class ControlHandler(
    IAuthService authService,
    ILogger<ControlHandler> logger)
{
    public Task HandleRequestAsync(string requestLine, CancellationToken ct)
    {
        ControlCommand? command;
        try
        {
            command = JsonSerializer.Deserialize(requestLine, ControlJsonContext.Default.ControlCommand);
        }
        catch (JsonException ex)
        {
            logger.ZLogWarning(ex, $"Daemon control pipe command was not JSON");
            return Task.CompletedTask;
        }

        if (command?.Method == IpcConstants.Methods.SignIn)
            _ = SignInAsync(ct);

        return Task.CompletedTask;
    }

    private async Task SignInAsync(CancellationToken ct)
    {
        try
        {
            var result = await authService.SignInAsync(ct).ConfigureAwait(false);
            if (!result.Success)
                logger.ZLogWarning($"Daemon control pipe sign-in failed: {result.Error}");
        }
        catch (Exception ex)
        {
            logger.ZLogWarning(ex, $"Daemon control pipe sign-in failed");
        }
    }
}
