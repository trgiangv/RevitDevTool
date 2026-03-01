namespace RevitDevTool.Scintilla.Core;

public sealed class ScintillaTheme
{
    public Color Text { get; init; }
    public Color SecondaryText { get; init; }
    public Color TertiaryText { get; init; }
    public Color Invalid { get; init; }
    public Color Null { get; init; }
    public Color Name { get; init; }
    public Color String { get; init; }
    public Color Number { get; init; }
    public Color Boolean { get; init; }
    public Color Scalar { get; init; }
    public Color TokenLink { get; init; }
    public Color TokenEmphasis { get; init; }
    public Color TokenClassified { get; init; }
    public Color SearchHighlight { get; init; }
    public Color Trace { get; init; }
    public Color Debug { get; init; }
    public Color Information { get; init; }
    public Color Warning { get; init; }
    public Color Error { get; init; }
    public Color Critical { get; init; }
    public Color Background { get; init; }
    public IReadOnlyDictionary<string, Style> CustomStyles { get; init; } = new Dictionary<string, Style>(StringComparer.OrdinalIgnoreCase);
    public bool IsDarkTheme => Background.GetBrightness() < 0.5f;

    public ScintillaTheme WithCustomStyle(string styleKey, Style style)
    {
        if (string.IsNullOrWhiteSpace(styleKey))
            return this;

        var styles = CreateMutableStyleMap(CustomStyles);
        styles[styleKey] = style;
        return CloneWithStyles(styles);
    }

    public ScintillaTheme WithCustomStyles(IReadOnlyDictionary<string, Style> styles)
    {
        var merged = CreateMutableStyleMap(CustomStyles);
        foreach (var pair in styles)
            merged[pair.Key] = pair.Value;
        return CloneWithStyles(merged);
    }

    private ScintillaTheme CloneWithStyles(IReadOnlyDictionary<string, Style> styles)
    {
        return new ScintillaTheme
        {
            Text = Text,
            SecondaryText = SecondaryText,
            TertiaryText = TertiaryText,
            Invalid = Invalid,
            Null = Null,
            Name = Name,
            String = String,
            Number = Number,
            Boolean = Boolean,
            Scalar = Scalar,
            TokenLink = TokenLink,
            TokenEmphasis = TokenEmphasis,
            TokenClassified = TokenClassified,
            SearchHighlight = SearchHighlight,
            Trace = Trace,
            Debug = Debug,
            Information = Information,
            Warning = Warning,
            Error = Error,
            Critical = Critical,
            Background = Background,
            CustomStyles = styles
        };
    }

    private static Dictionary<string, Style> CreateMutableStyleMap(IReadOnlyDictionary<string, Style> source)
    {
        var map = new Dictionary<string, Style>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in source)
            map[pair.Key] = pair.Value;
        return map;
    }

    public static ScintillaTheme Dark { get; } = new()
    {
        Text = Color.Gainsboro,
        SecondaryText = Color.DarkGray,
        TertiaryText = Color.Gray,
        Invalid = Color.Gold,
        Null = Color.LightSteelBlue,
        Name = Color.Plum,
        String = Color.PaleGreen,
        Number = Color.BurlyWood,
        Boolean = Color.LightBlue,
        Scalar = Color.PaleTurquoise,
        TokenLink = Color.FromArgb(120, 170, 255),
        TokenEmphasis = Color.FromArgb(255, 220, 70),
        TokenClassified = Color.FromArgb(120, 255, 170),
        SearchHighlight = Color.FromArgb(255, 213, 79),
        Trace = Color.DarkGray,
        Debug = Color.LightGray,
        Information = Color.Gainsboro,
        Warning = Color.Gold,
        Error = Color.IndianRed,
        Critical = Color.White,
        Background = Color.FromArgb(30, 30, 30)
    };

    public static ScintillaTheme EnhancedDark { get; } = ThemePresets.EnhancedDark;
    public static ScintillaTheme EnhancedLight { get; } = ThemePresets.EnhancedLight;
}
