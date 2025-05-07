using MaterialDesignThemes.Wpf;

namespace RevitDevTool.Theme;

public class ThemeBundle : BundledTheme
{
    public ThemeBundle()
    {
#if REVIT2024_OR_GREATER
        BaseTheme = Autodesk.Revit.UI.UIThemeManager.CurrentTheme switch
        {
            Autodesk.Revit.UI.UITheme.Light => MaterialDesignThemes.Wpf.BaseTheme.Light,
            Autodesk.Revit.UI.UITheme.Dark => MaterialDesignThemes.Wpf.BaseTheme.Dark,
            _ => throw new ArgumentOutOfRangeException()
        };
#else
        BaseTheme = MaterialDesignThemes.Wpf.BaseTheme.Light;
#endif
        PrimaryColor = MaterialDesignColors.PrimaryColor.Orange;
        SecondaryColor = MaterialDesignColors.SecondaryColor.Amber;
        ColorAdjustment = new ColorAdjustment();
    }
}