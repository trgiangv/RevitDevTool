using RevitDevTool.Scintilla.Core;
namespace RevitDevTool.Scintilla.Render;

public interface ILogThemeProvider
{
    ScintillaTheme CurrentTheme { get; }
    event EventHandler? ThemeChanged;
    bool TrySetTheme(ScintillaTheme theme);
}
