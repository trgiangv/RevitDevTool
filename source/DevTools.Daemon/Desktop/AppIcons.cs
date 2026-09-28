using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DevTools.Daemon.Desktop;

internal static class AppIcons
{
    private const string DarkResourceName = "DevTools.Daemon.Icons.Dark.ico";
    private const string LightResourceName = "DevTools.Daemon.Icons.Light.ico";

    public static ImageSource ForTheme(bool light) =>
        LoadImage(light ? LightResourceName : DarkResourceName);

    public static ImageSource LoadImage(string name)
    {
        using var stream = Open(name);
        var image = new BitmapImage();
        image.BeginInit();
        image.StreamSource = stream;
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        return image;
    }

    public static Stream Open(string name) =>
        typeof(AppIcons).Assembly.GetManifestResourceStream(name)
        ?? throw new InvalidOperationException($"Embedded icon '{name}' was not found.");
}
