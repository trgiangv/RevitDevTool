namespace RevitDevTool.Scintilla.Core;

public static class ThemePresets
{
    private static readonly Color LightBackground = Color.FromArgb(250, 250, 250);
    private static readonly Color DarkBackground = Color.FromArgb(37, 37, 37);

    public static ScintillaTheme EnhancedDark { get; } = new()
    {
        Text = Color.FromArgb(240, 240, 240),
        SecondaryText = Color.FromArgb(180, 180, 180),
        TertiaryText = Color.FromArgb(160, 160, 160),
        Invalid = Color.FromArgb(255, 220, 120),
        Null = Color.FromArgb(180, 180, 255),
        Name = Color.FromArgb(255, 200, 255),
        String = Color.FromArgb(120, 255, 170),
        Number = Color.FromArgb(255, 200, 120),
        Boolean = Color.FromArgb(170, 220, 255),
        Scalar = Color.FromArgb(170, 255, 220),
        TokenLink = Color.FromArgb(120, 170, 255),
        TokenEmphasis = Color.FromArgb(255, 220, 70),
        TokenClassified = Color.FromArgb(120, 255, 170),
        SearchHighlight = Color.FromArgb(255, 213, 79),
        Trace = Color.FromArgb(140, 140, 140),
        Debug = Color.FromArgb(170, 170, 170),
        Information = Color.FromArgb(120, 170, 255),
        Warning = Color.FromArgb(255, 220, 70),
        Error = Color.FromArgb(255, 100, 100),
        Critical = Color.FromArgb(255, 80, 80),
        Background = DarkBackground
    };

    public static ScintillaTheme EnhancedLight { get; } = new()
    {
        Text = Color.FromArgb(40, 40, 40),
        SecondaryText = Color.FromArgb(80, 80, 80),
        TertiaryText = Color.FromArgb(120, 120, 120),
        Invalid = Color.FromArgb(180, 80, 0),
        Null = Color.FromArgb(80, 80, 200),
        Name = Color.FromArgb(150, 0, 150),
        String = Color.FromArgb(0, 120, 0),
        Number = Color.FromArgb(180, 80, 0),
        Boolean = Color.FromArgb(0, 80, 180),
        Scalar = Color.FromArgb(0, 140, 100),
        TokenLink = Color.FromArgb(0, 80, 180),
        TokenEmphasis = Color.FromArgb(200, 140, 0),
        TokenClassified = Color.FromArgb(0, 120, 0),
        SearchHighlight = Color.FromArgb(0, 110, 220),
        Trace = Color.FromArgb(120, 120, 120),
        Debug = Color.FromArgb(80, 80, 80),
        Information = Color.FromArgb(0, 80, 180),
        Warning = Color.FromArgb(200, 140, 0),
        Error = Color.FromArgb(200, 60, 60),
        Critical = Color.FromArgb(150, 30, 30),
        Background = LightBackground
    };

    public static ScintillaTheme SoftDark { get; } = new()
    {
        Text = Color.FromArgb(200, 200, 200),
        SecondaryText = Color.FromArgb(150, 150, 150),
        TertiaryText = Color.FromArgb(120, 120, 120),
        Invalid = Color.FromArgb(220, 180, 80),
        Null = Color.FromArgb(130, 130, 220),
        Name = Color.FromArgb(220, 160, 220),
        String = Color.FromArgb(120, 200, 140),
        Number = Color.FromArgb(220, 160, 100),
        Boolean = Color.FromArgb(130, 180, 220),
        Scalar = Color.FromArgb(140, 200, 180),
        TokenLink = Color.FromArgb(120, 160, 220),
        TokenEmphasis = Color.FromArgb(220, 180, 60),
        TokenClassified = Color.FromArgb(120, 200, 140),
        SearchHighlight = Color.FromArgb(255, 200, 90),
        Trace = Color.FromArgb(100, 100, 100),
        Debug = Color.FromArgb(130, 130, 130),
        Information = Color.FromArgb(120, 160, 220),
        Warning = Color.FromArgb(220, 180, 60),
        Error = Color.FromArgb(180, 60, 60),
        Critical = Color.FromArgb(150, 40, 40),
        Background = DarkBackground
    };

    public static ScintillaTheme HighContrastLight { get; } = new()
    {
        Text = Color.FromArgb(20, 20, 20),
        SecondaryText = Color.FromArgb(60, 60, 60),
        TertiaryText = Color.FromArgb(100, 100, 100),
        Invalid = Color.FromArgb(180, 80, 0),
        Null = Color.FromArgb(60, 60, 180),
        Name = Color.FromArgb(120, 0, 120),
        String = Color.FromArgb(0, 100, 0),
        Number = Color.FromArgb(160, 60, 0),
        Boolean = Color.FromArgb(0, 60, 160),
        Scalar = Color.FromArgb(0, 120, 80),
        TokenLink = Color.FromArgb(0, 60, 160),
        TokenEmphasis = Color.FromArgb(255, 200, 80),
        TokenClassified = Color.FromArgb(0, 100, 0),
        SearchHighlight = Color.FromArgb(0, 90, 200),
        Trace = Color.FromArgb(100, 100, 100),
        Debug = Color.FromArgb(60, 60, 60),
        Information = Color.FromArgb(0, 60, 160),
        Warning = Color.FromArgb(255, 200, 80),
        Error = Color.FromArgb(180, 40, 40),
        Critical = Color.FromArgb(120, 20, 20),
        Background = LightBackground
    };
}
