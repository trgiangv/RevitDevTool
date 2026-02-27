using System.Drawing;
using RevitDevTool.Scintilla.Contracts;
using ScintillaNET;

namespace RevitDevTool.Scintilla.WinForms;

internal sealed class ScintillaStyleWriter(ScintillaNET.Scintilla scintilla) : IStyleWriter
{
    private readonly ScintillaNET.Scintilla _scintilla = scintilla;

    public void SetDefaultStyle(string fontName, int fontSize, Color foreColor, Color backColor)
    {
        var defaultStyle = _scintilla.Styles[Style.Default];
        defaultStyle.Font = fontName;
        defaultStyle.Size = fontSize;
        defaultStyle.ForeColor = foreColor;
        defaultStyle.BackColor = backColor;
        _scintilla.StyleClearAll();
    }

    public void SetStyle(int styleId, Color foreColor, Color backColor, bool bold = false)
    {
        var style = _scintilla.Styles[styleId];
        style.ForeColor = foreColor;
        style.BackColor = backColor;
        style.Bold = bold;
    }
}
