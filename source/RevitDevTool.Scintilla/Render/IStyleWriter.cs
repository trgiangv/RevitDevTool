namespace RevitDevTool.Scintilla.Render;

public interface IStyleWriter
{
    void SetDefaultStyle(string fontName, int fontSize, Color foreColor, Color backColor);
    void SetStyle(int styleId, Color foreColor, Color backColor, bool bold = false);
}
