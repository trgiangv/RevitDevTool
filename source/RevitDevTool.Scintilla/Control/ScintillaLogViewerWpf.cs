using System.Windows;
using System.Windows.Forms.Integration;
using RevitDevTool.Scintilla.Core;
using RevitDevTool.Scintilla.Render;

namespace RevitDevTool.Scintilla.Control;

public sealed class ScintillaLogViewerWpf : IScintillaLogViewHost, IDisposable
{
    private readonly ScintillaLogViewer _winForms;
    private readonly WindowsFormsHost _wpfHost;

    public ScintillaLogViewerWpf(
        ScintillaLogViewerOptions? options = null,
        ILogRenderStrategy? renderStrategy = null)
    {
        _winForms = new ScintillaLogViewer(options, renderStrategy);
        _wpfHost = new WindowsFormsHost
        {
            Child = _winForms.ScintillaControl
        };
    }

    public FrameworkElement HostElement => _wpfHost;
    public ILogViewerController Controller => _winForms.Controller;
    public ScintillaNET.Scintilla ScintillaControl => _winForms.ScintillaControl;
    public ILogThemeProvider ThemeProvider => _winForms.ThemeProvider;

    public void RefreshStyles() => _winForms.RefreshStyles();
    public bool TrySetTheme(ScintillaTheme theme) => _winForms.TrySetTheme(theme);

    public void Dispose()
    {
        _winForms.Dispose();
        _wpfHost.Dispose();
    }
}
