using System.Windows.Forms.Integration;
using AcadDevTool.View;
using Autodesk.AutoCAD.Windows;
using DevTools.Logging.Listeners;
namespace AcadDevTool.Controllers;

public sealed class PanelController
{
    private static readonly Guid PaletteGuid = new("B7F3E2A1-4C8D-4F5E-9A1B-2D3E4F5A6B7C");

    private PaletteSet? _paletteSet;
    private MainPage? _mainPage;

    public void Initialize()
    {
        if (_paletteSet is not null) return;

        _mainPage = new MainPage();

        _paletteSet = new PaletteSet("DevTools", PaletteGuid)
        {
            Style = PaletteSetStyles.ShowAutoHideButton
                    | PaletteSetStyles.ShowCloseButton
                    | PaletteSetStyles.Snappable,
            MinimumSize = new Size(400, 300),
            DockEnabled = DockSides.Left | DockSides.Right,
            Dock = DockSides.Right,
            KeepFocus = true
        };

        var host = new ElementHost { Child = _mainPage, Dock = DockStyle.Fill };
        _paletteSet.Add("DevTools", host);

        NotifyListener.TraceReceived += OnTraceReceived;
    }

    public void Show()
    {
        if (_paletteSet is null) Initialize();
        _paletteSet!.Visible = true;
    }

    public void ToggleVisibility()
    {
        if (_paletteSet is null)
        {
            Initialize();
            _paletteSet!.Visible = true;
            return;
        }

        _paletteSet.Visible = !_paletteSet.Visible;
    }

    public void Shutdown()
    {
        NotifyListener.TraceReceived -= OnTraceReceived;

        if (_paletteSet is null) return;

        _paletteSet.Visible = false;
        _paletteSet.Dispose();
        _paletteSet = null;
        _mainPage = null;
    }

    private void OnTraceReceived()
    {
        if (_paletteSet is null || _paletteSet.Visible) return;
        _paletteSet.Visible = true;
    }
}
