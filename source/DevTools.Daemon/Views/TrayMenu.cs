using System.Runtime.InteropServices;
using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using DevTools.Daemon.Desktop;
using H.NotifyIcon.Core;
using DrawingIcon = System.Drawing.Icon;
using DrawingPoint = System.Drawing.Point;

namespace DevTools.Daemon.Views;

public sealed class TrayMenu : IDisposable
{
    private const string DefaultStatusText = "DevTools Daemon";

    private readonly AppState _state;
    private readonly MainWindow _window;
    private readonly TrayMenuHost _host;

    private TrayIcon? _tray;
    private DrawingIcon? _icon;
    private bool _menuOpen;
    private bool _exiting;
    private bool _disposed;

    public TrayMenu(AppState state, MainWindow window)
    {
        _state = state;
        _window = window;
        _host = new TrayMenuHost(
            open: ShowMainWindow,
            signIn: () => _ = _state.SignIn(),
            canSignIn: () => !_state.IsAuthenticated.Value,
            signOut: () => _ = _state.SignOut(),
            canSignOut: () => _state.IsAuthenticated.Value,
            quit: Quit);
        _host.Deactivated += OnHostDeactivated;
        _host.Closing += OnHostClosing;
    }

    public void Start()
    {
        _tray = new TrayIcon { ToolTip = DefaultStatusText };
        ApplyIcon();
        _tray.MessageWindow.MouseEventReceived += OnMouseEvent;
        _tray.MessageWindow.KeyboardEventReceived += OnKeyboardEvent;
        _tray.Create();

        RealizeHost();

        ThemeHelper.Changed += ApplyIcon;
    }

    public void ShowMainWindow()
    {
        UiDispatch.Send(() =>
        {
            HideMenu();
            Application.Current.MainWindow = _window;
            _window.Show();
            _window.Activate();
        });
    }

    private void RealizeHost()
    {
        _host.Show();
        AttachToTrayMessageWindow();
        _host.Hide();
    }

    private void AttachToTrayMessageWindow()
    {
        if (_host.Handle == 0 || _tray is not { WindowHandle: not 0 } tray)
            return;

        SetOwnerWindow(_host.Handle, tray.WindowHandle);
    }

    private void ShowMenu(DrawingPoint cursor)
    {
        if (_host.Handle == 0)
            RealizeHost();

        var scale = Math.Max(_host.DpiScale, 1.0);
        _host.MoveTo(cursor.X / scale, cursor.Y / scale);
        _menuOpen = true;
        _host.Show();

        var client = _host.ScreenToClient(new Point(cursor.X, cursor.Y));
        _host.OpenMenu(client);
        WindowUtilities.SetForegroundWindow(_host.Handle);
    }

    private void HideMenu()
    {
        _menuOpen = false;
        if (_host.Handle == 0)
            return;

        _host.Hide();
    }

    private void OnHostDeactivated()
    {
        if (_menuOpen)
            HideMenu();
    }

    private void OnHostClosing(ClosingEventArgs e)
    {
        if (_exiting)
            return;
        e.Cancel = true;
        HideMenu();
    }

    private void OnMouseEvent(object? sender, MessageWindow.MouseEventReceivedEventArgs e)
    {
        switch (e.MouseEvent)
        {
            case MouseEvent.IconLeftMouseUp:
                ShowMainWindow();
                break;
            case MouseEvent.IconRightMouseUp:
                UiDispatch.Post(() => ShowMenu(e.Point));
                break;
        }
    }

    private void OnKeyboardEvent(object? sender, MessageWindow.KeyboardEventReceivedEventArgs e)
    {
        if (e.KeyboardEvent == KeyboardEvent.ContextMenu)
            UiDispatch.Post(() => ShowMenu(e.Point));
    }

    private void Quit()
    {
        _exiting = true;
        HideMenu();
        Application.Shutdown();
    }

    private void ApplyIcon()
    {
        if (_tray is null)
            return;

        var next = AppIcons.TrayIcon(ThemeHelper.IsLight(_state.Preferences.Theme.Value));
        _tray.Icon = next.Handle;
        _icon?.Dispose();
        _icon = next;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _exiting = true;

        ThemeHelper.Changed -= ApplyIcon;
        _host.Deactivated -= OnHostDeactivated;
        _host.Closing -= OnHostClosing;

        if (_tray is not null)
        {
            _tray.MessageWindow.MouseEventReceived -= OnMouseEvent;
            _tray.MessageWindow.KeyboardEventReceived -= OnKeyboardEvent;
            _tray.Dispose();
            _tray = null;
        }

        _icon?.Dispose();
        _icon = null;
        _host.Close();
    }

    private static void SetOwnerWindow(nint hwnd, nint owner)
    {
        const int gwlpHwndParent = -8;
        const uint swpNoSize = 0x0001;
        const uint swpNoMove = 0x0002;
        const uint swpNoZOrder = 0x0004;
        const uint swpNoActivate = 0x0010;
        const uint swpFrameChanged = 0x0020;
        SetWindowLongPtr(hwnd, gwlpHwndParent, owner);
        SetWindowPos(hwnd, 0, 0, 0, 0, 0, swpNoSize | swpNoMove | swpNoZOrder | swpNoActivate | swpFrameChanged);
    }

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(nint hWnd, nint hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

    private sealed class TrayMenuHost : Window
    {
        private readonly ContextMenu _menu;

        public TrayMenuHost(
            Action open,
            Action signIn,
            Func<bool> canSignIn,
            Action signOut,
            Func<bool> canSignOut,
            Action quit)
        {
            Title = DefaultStatusText;
            ShowInTaskbar = false;
            Topmost = true;
            Borderless = true;
            Opacity = 0;
            IsHitTestVisible = false;
            this.Fixed(1, 1).StartManualPosition(-32000, -32000);

            var openCommand = new Command("tray.open", "Open");
            var signInCommand = new Command("tray.signIn", "Sign In");
            var signOutCommand = new Command("tray.signOut", "Sign Out");
            var quitCommand = new Command("tray.quit", "Quit");
            Commands.Register(openCommand, open);
            Commands.Register(signInCommand, signIn, canSignIn);
            Commands.Register(signOutCommand, signOut, canSignOut);
            Commands.Register(quitCommand, quit);

            _menu = new ContextMenu()
                .MinWidth(160)
                .BorderThickness(0)
                .BorderBrush(Color.Transparent)
                .ItemPadding(new Thickness(16, 6, 16, 6))
                .Item(openCommand)
                .Item(signInCommand)
                .Item(signOutCommand)
                .Item(quitCommand);
        }

        public void OpenMenu(Point clientPoint) => _menu.ShowAt(this, clientPoint);
    }
}
