using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DevTools.Daemon.Control;
using Microsoft.Extensions.Logging;
using ZLogger;

namespace DevTools.Daemon.Auth;

internal sealed class TokenStore(string path, ILogger logger)
{
    public TokenData? TryLoad()
    {
        try
        {
            if (!File.Exists(path))
                return null;

            var encrypted = File.ReadAllBytes(path);
            var plainBytes = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            return JsonSerializer.Deserialize(Encoding.UTF8.GetString(plainBytes), ControlJsonContext.Default.TokenData);
        }
        catch (Exception ex)
        {
            logger.ZLogWarning(ex, $"Failed to load stored auth token");
            return null;
        }
    }

    public void Save(TokenData data)
    {
        try
        {
            var json = JsonSerializer.Serialize(data, ControlJsonContext.Default.TokenData);
            var encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(json), null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(path, encrypted);
        }
        catch (Exception ex)
        {
            logger.ZLogWarning(ex, $"Failed to save auth token");
        }
    }

    public void Delete()
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            /* best-effort */
        }
    }
}
