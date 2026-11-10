using DevTools.Daemon.Control;
using Microsoft.Win32;

namespace DevTools.Daemon.Tests.Support;

/// <summary>
///     A settings file and an HKCU key of one test, so no test touches the real <c>settings.json</c> or the Windows <c>Run</c> key.
/// </summary>
internal sealed class TempUserState : IDisposable
{
    private readonly string _id = Guid.NewGuid().ToString("N");

    public TempUserState()
    {
        Startup = new StartupRegistration($@"Software\RevitDevTool.Tests.{_id}");
        SettingsPath = Path.Combine(Path.GetTempPath(), $"RevitDevTool.Tests.{_id}.json");
    }

    public StartupRegistration Startup { get; }

    public string SettingsPath { get; }

    public void Dispose()
    {
        Registry.CurrentUser.DeleteSubKeyTree(Startup.KeyPath, throwOnMissingSubKey: false);
        File.Delete(SettingsPath);
    }
}
