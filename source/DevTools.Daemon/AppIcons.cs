using System.Windows.Media;
using System.Windows.Media.Imaging;
using ControlzEx.Theming;

namespace DevTools.Daemon;

public static class AppIcons
{
    private const string DarkIconUri = "pack://application:,,,/DevTools.UI;component/Resources/icons/DevTools-32-Dark.ico";
    private const string LightIconUri = "pack://application:,,,/DevTools.UI;component/Resources/icons/DevTools-32-Light.ico";

    public static ImageSource Dark { get; } = CreateFrozen(DarkIconUri);
    public static ImageSource Light { get; } = CreateFrozen(LightIconUri);

    public static ImageSource ForCurrentTheme()
    {
        var isLight = WindowsThemeHelper.AppsUseLightTheme();
        return isLight ? Light : Dark;
    }

    private static BitmapImage CreateFrozen(string uri)
    {
        var image = new BitmapImage(new Uri(uri, UriKind.Absolute));
        image.Freeze();
        return image;
    }
}
