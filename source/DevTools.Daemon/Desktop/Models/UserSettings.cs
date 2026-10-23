using DevTools.Settings.Configs;
using DevTools.Utilities;
using System.IO;

namespace DevTools.Daemon.Desktop.Models;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed class UserSettings
{
    public const string SectionName = "User";
    public const string FileName = "settings.json";

    public static string FilePath =>
        Path.Combine(AppUtils.GetApplicationDataPath(), FileName);

    public AppTheme Theme { get; set; } = AppTheme.Auto;
    public bool AutoStartEnabled { get; set; }
}
