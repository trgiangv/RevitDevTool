namespace ZLogger.RichTextBox.Winforms.Themes;

public static class ThemePresets
{
    public static Theme Literate { get; } = new(
        new Style(ThemeColors.White, ThemeColors.Black),
        new Dictionary<StyleToken, Style>
        {
            [StyleToken.Text] = new(ThemeColors.White, ThemeColors.Black),
            [StyleToken.SecondaryText] = new(ThemeColors.Gray, ThemeColors.Black),
            [StyleToken.TertiaryText] = new(ThemeColors.Gray, ThemeColors.Black),
            [StyleToken.Invalid] = new(ThemeColors.Yellow, ThemeColors.Black),
            [StyleToken.Null] = new(ThemeColors.LightBlue, ThemeColors.Black),
            [StyleToken.Name] = new(ThemeColors.Gray, ThemeColors.Black),
            [StyleToken.String] = new(ThemeColors.Cyan, ThemeColors.Black),
            [StyleToken.Number] = new(ThemeColors.Magenta, ThemeColors.Black),
            [StyleToken.Boolean] = new(ThemeColors.LightBlue, ThemeColors.Black),
            [StyleToken.Scalar] = new(ThemeColors.Green, ThemeColors.Black),
            [StyleToken.LevelVerbose] = new(ThemeColors.Gray, ThemeColors.Black),
            [StyleToken.LevelDebug] = new(ThemeColors.Gray, ThemeColors.Black),
            [StyleToken.LevelInformation] = new(ThemeColors.White, ThemeColors.Black),
            [StyleToken.LevelWarning] = new(ThemeColors.Yellow, ThemeColors.Black),
            [StyleToken.LevelError] = new(ThemeColors.White, ThemeColors.Red),
            [StyleToken.LevelFatal] = new(ThemeColors.White, ThemeColors.Red)
        });

    public static Theme Grayscale { get; } = new(
        new Style(ThemeColors.White, ThemeColors.Black),
        new Dictionary<StyleToken, Style>
        {
            [StyleToken.Text] = new(ThemeColors.White, ThemeColors.Black),
            [StyleToken.SecondaryText] = new(ThemeColors.Gray, ThemeColors.Black),
            [StyleToken.TertiaryText] = new(ThemeColors.Gray, ThemeColors.Black),
            [StyleToken.Invalid] = new(ThemeColors.White, ThemeColors.DarkGray),
            [StyleToken.Null] = new(ThemeColors.White, ThemeColors.Black),
            [StyleToken.Name] = new(ThemeColors.Gray, ThemeColors.Black),
            [StyleToken.String] = new(ThemeColors.White, ThemeColors.Black),
            [StyleToken.Number] = new(ThemeColors.White, ThemeColors.Black),
            [StyleToken.Boolean] = new(ThemeColors.White, ThemeColors.Black),
            [StyleToken.Scalar] = new(ThemeColors.White, ThemeColors.Black),
            [StyleToken.LevelVerbose] = new(ThemeColors.Gray, ThemeColors.Black),
            [StyleToken.LevelDebug] = new(ThemeColors.Gray, ThemeColors.Black),
            [StyleToken.LevelInformation] = new(ThemeColors.White, ThemeColors.Black),
            [StyleToken.LevelWarning] = new(ThemeColors.White, ThemeColors.DarkGray),
            [StyleToken.LevelError] = new(ThemeColors.Black, ThemeColors.White),
            [StyleToken.LevelFatal] = new(ThemeColors.Black, ThemeColors.White)
        });

    public static Theme Colored { get; } = new(
        new Style(ThemeColors.Gray, ThemeColors.Black),
        new Dictionary<StyleToken, Style>
        {
            [StyleToken.Text] = new(ThemeColors.Gray, ThemeColors.Black),
            [StyleToken.SecondaryText] = new(ThemeColors.Gray, ThemeColors.Black),
            [StyleToken.TertiaryText] = new(ThemeColors.Gray, ThemeColors.Black),
            [StyleToken.Invalid] = new(ThemeColors.Yellow, ThemeColors.Black),
            [StyleToken.Null] = new(ThemeColors.White, ThemeColors.Black),
            [StyleToken.Name] = new(ThemeColors.White, ThemeColors.Black),
            [StyleToken.String] = new(ThemeColors.White, ThemeColors.Black),
            [StyleToken.Number] = new(ThemeColors.White, ThemeColors.Black),
            [StyleToken.Boolean] = new(ThemeColors.White, ThemeColors.Black),
            [StyleToken.Scalar] = new(ThemeColors.White, ThemeColors.Black),
            [StyleToken.LevelVerbose] = new(ThemeColors.Gray, ThemeColors.DarkGray),
            [StyleToken.LevelDebug] = new(ThemeColors.White, ThemeColors.DarkGray),
            [StyleToken.LevelInformation] = new(ThemeColors.White, ThemeColors.Blue),
            [StyleToken.LevelWarning] = new(ThemeColors.DarkGray, ThemeColors.Yellow),
            [StyleToken.LevelError] = new(ThemeColors.White, ThemeColors.Red),
            [StyleToken.LevelFatal] = new(ThemeColors.White, ThemeColors.Red)
        });

    public static Theme Luminous { get; } = new(
        new Style(ThemeColors.Black, ThemeColors.White),
        new Dictionary<StyleToken, Style>
        {
            [StyleToken.Text] = new(ThemeColors.Black, ThemeColors.White),
            [StyleToken.SecondaryText] = new(ThemeColors.DarkGray, ThemeColors.White),
            [StyleToken.TertiaryText] = new(ThemeColors.DarkGray, ThemeColors.White),
            [StyleToken.Invalid] = new(ThemeColors.White, ThemeColors.Red),
            [StyleToken.Null] = new(ThemeColors.DarkBlue, ThemeColors.White),
            [StyleToken.Name] = new(ThemeColors.DarkMagenta, ThemeColors.White),
            [StyleToken.String] = new(ThemeColors.DarkGreen, ThemeColors.White),
            [StyleToken.Number] = new(ThemeColors.DarkCyan, ThemeColors.White),
            [StyleToken.Boolean] = new(ThemeColors.DarkYellow, ThemeColors.White),
            [StyleToken.Scalar] = new(ThemeColors.DarkCyan, ThemeColors.White),
            [StyleToken.LevelVerbose] = new(ThemeColors.DarkGray, ThemeColors.White),
            [StyleToken.LevelDebug] = new(ThemeColors.DarkBlue, ThemeColors.White),
            [StyleToken.LevelInformation] = new(ThemeColors.DarkGreen, ThemeColors.White),
            [StyleToken.LevelWarning] = new(ThemeColors.Black, ThemeColors.Yellow),
            [StyleToken.LevelError] = new(ThemeColors.White, ThemeColors.Red),
            [StyleToken.LevelFatal] = new(ThemeColors.White, ThemeColors.Red)
        });

    public static Theme EnhancedDark { get; } = new(
        new Style(ThemeColors.White, ThemeColors.DarkBackground),
        new Dictionary<StyleToken, Style>
        {
            [StyleToken.Text] = new(ThemeColors.White, ThemeColors.DarkBackground),
            [StyleToken.SecondaryText] = new(ThemeColors.Gray, ThemeColors.DarkBackground),
            [StyleToken.TertiaryText] = new(ThemeColors.Gray, ThemeColors.DarkBackground),
            [StyleToken.Invalid] = new(ThemeColors.Yellow, ThemeColors.DarkBackground),
            [StyleToken.Null] = new(ThemeColors.LightBlue, ThemeColors.DarkBackground),
            [StyleToken.Name] = new(ThemeColors.Gray, ThemeColors.DarkBackground),
            [StyleToken.String] = new(ThemeColors.Cyan, ThemeColors.DarkBackground),
            [StyleToken.Number] = new(ThemeColors.Magenta, ThemeColors.DarkBackground),
            [StyleToken.Boolean] = new(ThemeColors.LightBlue, ThemeColors.DarkBackground),
            [StyleToken.Scalar] = new(ThemeColors.Green, ThemeColors.DarkBackground),
            [StyleToken.LevelVerbose] = new(ThemeColors.Gray, ThemeColors.DarkBackground),
            [StyleToken.LevelDebug] = new(ThemeColors.Gray, ThemeColors.DarkBackground),
            [StyleToken.LevelInformation] = new(ThemeColors.White, ThemeColors.DarkBackground),
            [StyleToken.LevelWarning] = new(ThemeColors.Yellow, ThemeColors.DarkBackground),
            [StyleToken.LevelError] = new(ThemeColors.White, ThemeColors.Red),
            [StyleToken.LevelFatal] = new(ThemeColors.White, ThemeColors.Red)
        });

    public static Theme EnhancedLight { get; } = new(
        new Style(ThemeColors.Black, ThemeColors.LightBackground),
        new Dictionary<StyleToken, Style>
        {
            [StyleToken.Text] = new(ThemeColors.Black, ThemeColors.LightBackground),
            [StyleToken.SecondaryText] = new(ThemeColors.DarkGray, ThemeColors.LightBackground),
            [StyleToken.TertiaryText] = new(ThemeColors.DarkGray, ThemeColors.LightBackground),
            [StyleToken.Invalid] = new(ThemeColors.White, ThemeColors.Red),
            [StyleToken.Null] = new(ThemeColors.DarkBlue, ThemeColors.LightBackground),
            [StyleToken.Name] = new(ThemeColors.DarkMagenta, ThemeColors.LightBackground),
            [StyleToken.String] = new(ThemeColors.DarkGreen, ThemeColors.LightBackground),
            [StyleToken.Number] = new(ThemeColors.DarkCyan, ThemeColors.LightBackground),
            [StyleToken.Boolean] = new(ThemeColors.DarkYellow, ThemeColors.LightBackground),
            [StyleToken.Scalar] = new(ThemeColors.DarkCyan, ThemeColors.LightBackground),
            [StyleToken.LevelVerbose] = new(ThemeColors.DarkGray, ThemeColors.LightBackground),
            [StyleToken.LevelDebug] = new(ThemeColors.DarkBlue, ThemeColors.LightBackground),
            [StyleToken.LevelInformation] = new(ThemeColors.DarkGreen, ThemeColors.LightBackground),
            [StyleToken.LevelWarning] = new(ThemeColors.Black, ThemeColors.Yellow),
            [StyleToken.LevelError] = new(ThemeColors.White, ThemeColors.Red),
            [StyleToken.LevelFatal] = new(ThemeColors.White, ThemeColors.Red)
        });
}