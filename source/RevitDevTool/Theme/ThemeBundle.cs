using Autodesk.Revit.UI;
using MaterialDesignThemes.Wpf;

namespace RevitDevTool.Theme;

public class ThemeBundle : BundledTheme
{
    public ThemeBundle()
    {
        BaseTheme = UIThemeManager.CurrentTheme switch
        {
            UITheme.Light => MaterialDesignThemes.Wpf.BaseTheme.Light,
            UITheme.Dark => MaterialDesignThemes.Wpf.BaseTheme.Dark,
            _ => throw new ArgumentOutOfRangeException()
        };
        PrimaryColor = MaterialDesignColors.PrimaryColor.Orange;
        SecondaryColor = MaterialDesignColors.SecondaryColor.Amber;
        ColorAdjustment = new ColorAdjustment();
    }
}