using RevitDevTool.Scintilla.Contracts;
using RevitDevTool.Scintilla.Core;
using RevitDevTool.Scintilla.WinForms;
using ScintillaNET;

namespace RevitDevTool.Scintilla;

public sealed class ScintillaLogViewerHost : IDisposable
{
    private readonly ScintillaNET.Scintilla _scintillaControl;

    public ScintillaLogViewerHost(
        ScintillaLogViewerOptions? options = null,
        ILogRenderStrategy? renderStrategy = null)
    {
        _scintillaControl = new ScintillaNET.Scintilla
        {
            Dock = DockStyle.Fill
        };

        var dispatcher = new WinFormsUiDispatcher(_scintillaControl);
        var backend = new ScintillaDocumentBackend(_scintillaControl);
        Controller = new ScintillaLogViewerController(backend, dispatcher, renderStrategy, options);
        HostControl = _scintillaControl;
    }

    public Control HostControl { get; }
    public ILogViewerController Controller { get; }

    public void Dispose()
    {
        Controller.Dispose();
    }
}
