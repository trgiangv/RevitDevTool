using RevitDevTool.Scintilla.Core;
using RevitDevTool.Scintilla.Helpers;
using RevitDevTool.Scintilla.Logger;
using RevitDevTool.Scintilla.Render;
using RevitDevTool.Scintilla.Services;

namespace RevitDevTool.Scintilla.Control;

public sealed class ScintillaLogViewer : IScintillaLogViewHost, IDisposable
{
    private readonly ScintillaNET.Scintilla _scintillaControl;
    private readonly IUiDispatcher _dispatcher;
    private readonly ScintillaDocumentBackend _backend;
    private readonly ILogRenderStrategy _renderStrategy;
    private readonly ILogThemeProvider _themeProvider;
    private bool _disposed;

    public ScintillaLogViewer(
        ScintillaLogViewerOptions? options = null,
        ILogRenderStrategy? renderStrategy = null)
    {
        var resolvedOptions = options ?? new ScintillaLogViewerOptions();
        _themeProvider = resolvedOptions.ThemeProvider ?? new StaticLogThemeProvider(resolvedOptions.Theme);
        _scintillaControl = new ScintillaNET.Scintilla
        {
            Dock = DockStyle.Fill
        };

        _dispatcher = new UiDispatcher(_scintillaControl);
        _backend = new ScintillaDocumentBackend(_scintillaControl, resolvedOptions, _themeProvider);
        _renderStrategy = renderStrategy ?? new LogRenderStrategy(
            resolvedOptions.FontFamily,
            resolvedOptions.FontSize,
            _themeProvider,
            resolvedOptions.StyleRegistry,
            resolvedOptions);
        Controller = new ScintillaLogViewerController(_backend, _dispatcher, _renderStrategy, resolvedOptions);
        HostControl = _scintillaControl;
        ApplyNativeThemeToControl();
        _themeProvider.ThemeChanged += OnThemeChanged;
    }

    public System.Windows.Forms.Control HostControl { get; }
    public ILogViewerController Controller { get; }
    public ScintillaNET.Scintilla ScintillaControl => _scintillaControl;
    public ILogThemeProvider ThemeProvider => _themeProvider;

    public void RefreshStyles()
    {
        if (_disposed)
            return;

        if (_dispatcher.CheckAccess())
            _backend.ConfigureStyles(_renderStrategy);
        else
            _dispatcher.Invoke(() => _backend.ConfigureStyles(_renderStrategy));
    }

    public bool TrySetTheme(ScintillaTheme theme)
    {
        return _themeProvider.TrySetTheme(theme);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _themeProvider.ThemeChanged -= OnThemeChanged;
        _scintillaControl.HandleCreated -= OnHandleCreatedApplyTheme;
        Controller.Dispose();
        _disposed = true;
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        RefreshStyles();
        ApplyNativeTheme();
    }

    private void ApplyNativeThemeToControl()
    {
        if (_scintillaControl.IsHandleCreated)
            ApplyNativeTheme();
        else
            _scintillaControl.HandleCreated += OnHandleCreatedApplyTheme;
    }

    private void OnHandleCreatedApplyTheme(object? sender, EventArgs e)
    {
        _scintillaControl.HandleCreated -= OnHandleCreatedApplyTheme;
        ApplyNativeTheme();
    }

    private void ApplyNativeTheme()
    {
        if (_disposed)
            return;

        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.Invoke(ApplyNativeTheme);
            return;
        }

        if (!_scintillaControl.IsHandleCreated)
            return;

        NativeThemeHelper.ApplyNativeTheme(_scintillaControl.Handle, _themeProvider.CurrentTheme.IsDarkTheme);
    }
}
