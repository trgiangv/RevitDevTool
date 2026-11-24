using System.Drawing;
using System.Globalization;
using System.Text.RegularExpressions;

namespace RevitDevTool.ModelsContext.UiTests;

internal static class ThemeColors
{
    private const string ColorsFile = @"libs\HandyControl\src\Shared\HandyControl_Shared\Themes\Basic\Colors\ColorsDark.xaml";

    public static Color Warning => Read("WarningColor");

    private static Color Read(string key)
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null && !File.Exists(Path.Combine(folder.FullName, "RevitDevTool.slnx")))
        {
            folder = folder.Parent;
        }

        Assert.IsNotNull(folder, "The repository root (RevitDevTool.slnx) was not found above the test output.");
        var text = File.ReadAllText(Path.Combine(folder!.FullName, ColorsFile));
        var match = Regex.Match(text, $"x:Key=\"{key}\">#([0-9a-fA-F]{{6}})<");
        Assert.IsTrue(match.Success, $"{key} was not found in {ColorsFile}.");
        var rgb = int.Parse(match.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
    }
}
