using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace RevitDevTool.Utils;

public class ImageUtils
{
    public static ImageSource GetResourceImage(string path)
    {
        return new BitmapImage(new Uri($"pack://application:,,,/{Assembly.GetExecutingAssembly().GetName().Name};component/{path}"));
    }
}