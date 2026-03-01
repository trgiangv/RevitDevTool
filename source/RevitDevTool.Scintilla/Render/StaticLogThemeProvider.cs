using RevitDevTool.Scintilla.Core;
namespace RevitDevTool.Scintilla.Render;

public sealed class StaticLogThemeProvider : ILogThemeProvider
{
    private ScintillaTheme _currentTheme;

    public StaticLogThemeProvider(ScintillaTheme? theme = null)
    {
        _currentTheme = theme ?? ScintillaTheme.Dark;
    }

    public ScintillaTheme CurrentTheme => _currentTheme;

    public event EventHandler? ThemeChanged;

    public bool TrySetTheme(ScintillaTheme theme)
    {
        if (ReferenceEquals(_currentTheme, theme))
            return false;

        _currentTheme = theme;
        ThemeChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }
}
