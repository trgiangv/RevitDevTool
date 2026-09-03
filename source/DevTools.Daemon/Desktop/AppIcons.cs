using Aprillz.MewUI;
using DrawingIcon = System.Drawing.Icon;

namespace DevTools.Daemon.Desktop;

internal static class AppIcons
{
    private const string DarkResourceName = "DevTools.Daemon.Icons.Dark.ico";
    private const string LightResourceName = "DevTools.Daemon.Icons.Light.ico";

    public static IconSource WindowIcon(bool light) =>
        IconSource.FromResource(typeof(AppIcons).Assembly, light ? LightResourceName : DarkResourceName);

    public static DrawingIcon TrayIcon(bool light)
    {
        var name = light ? LightResourceName : DarkResourceName;
        var stream = typeof(AppIcons).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded icon '{name}' was not found.");
        return new DrawingIcon(stream);
    }
}
