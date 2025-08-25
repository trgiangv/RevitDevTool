using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using RevitDevTool.Models;
using RevitDevTool.Theme;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Sinks.RichTextBoxForms;
using Serilog.Sinks.RichTextBoxForms.Themes;
using WinFormsComboBox = System.Windows.Forms.ComboBox;
using WinFormsPanel = System.Windows.Forms.Panel;
using WinFormsButton = System.Windows.Forms.Button;
using WinFormsCheckBox = System.Windows.Forms.CheckBox;
using WinFormsRichTextBox = System.Windows.Forms.RichTextBox;
#if REVIT2024_OR_GREATER
using Autodesk.Revit.UI;
#endif

namespace RevitDevTool.View;

/// <summary>
/// A WinForms UserControl for displaying trace logs with theme support
/// </summary>
[DesignerCategory("UserControl")]
[Description("Trace log control with theme support and Serilog integration")]
[ToolboxItem(true)]
[ToolboxBitmap(typeof(UserControl))]
public partial class TraceLogControl : UserControl
{
    // UI Controls - Using standard WinForms controls with enhanced theme support
    private WinFormsComboBox _logLevelComboBox = null!;
    private WinFormsButton _clearLogButton = null!;
    private WinFormsButton _clearGeometryButton = null!;
    private WinFormsCheckBox _startStopToggle = null!;
    private WinFormsRichTextBox _logTextBox = null!;
    private TableLayoutPanel _mainPanel = null!;
    private TableLayoutPanel _topPanel = null!;
    private FlowLayoutPanel _buttonPanel = null!;
    private WinFormsPanel _logPanel = null!;

    // ViewModel properties
    private readonly LoggingLevelSwitch _levelSwitch;
    private readonly ConsoleRedirector _consoleRedirector;
    private SerilogTraceListener? _traceListener;
    private RichTextBoxSink? _sink;
    private Logger? _logger;

    private bool _isStarted = true;
    private LogEventLevel _logLevel = LogEventLevel.Debug;

    private static bool IsDarkTheme => ApplicationThemeManager.GetAppTheme() == ApplicationTheme.Dark;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsStarted
    {
        get => _isStarted;
        set
        {
            if (_isStarted != value)
            {
                _isStarted = value;
                OnPropertyChanged();
                OnIsStartedChanged(value);
            }
        }
    }

    public LogEventLevel LogLevel
    {
        get => _logLevel;
        set
        {
            if (_logLevel != value)
            {
                _logLevel = value;
                OnPropertyChanged();
                OnLogLevelChanged(value);
            }
        }
    }

    public TraceLogControl()
    {
        InitializeComponent();

        // Initialize required fields for both design-time and runtime
        _levelSwitch = new LoggingLevelSwitch(_logLevel);
        _consoleRedirector = new ConsoleRedirector();

        // Check if we're in design mode to avoid runtime-only initialization
        if (!DesignMode && !LicenseManager.UsageMode.Equals(LicenseUsageMode.Designtime))
        {
            // Set up ComboBox data source for runtime
            _logLevelComboBox.DataSource = Enum.GetValues(typeof(LogEventLevel));
            _logLevelComboBox.SelectedItem = _logLevel;

            SetupEventHandlers();
            SetupTooltips();

#if REVIT2024_OR_GREATER
            try
            {
                UIFramework.ApplicationTheme.CurrentTheme.PropertyChanged += ApplyRevitTheme;
            }
            catch
            {
                // Ignore if Revit theme is not available (e.g., in designer)
            }
#endif
            ApplicationThemeManager.Changed += OnThemeChanged;

            // Initialize theme based on current Revit theme
            InitializeTheme();

            TraceStatus(IsStarted);
        }
        else
        {
            // Design-time initialization
            SetupDesignTimeData();
        }

        ApplyTheme();
    }

    private void SetupEventHandlers()
    {
        _logLevelComboBox.SelectedIndexChanged += OnLogLevelChanged;
        _clearLogButton.Click += OnClearLogClick;
        _clearGeometryButton.Click += OnClearGeometryClick;
        _startStopToggle.CheckedChanged += OnStartStopToggleChanged;

        // Add keyboard shortcuts
        KeyDown += OnKeyDown;

        // Improve accessibility
        _logLevelComboBox.AccessibleName = "Log Level Selector";
        _logLevelComboBox.AccessibleDescription = "Select the minimum log level to display";
        _clearLogButton.AccessibleName = "Clear Log Button";
        _clearLogButton.AccessibleDescription = "Clear all log entries";
        _clearGeometryButton.AccessibleName = "Clear Geometry Button";
        _clearGeometryButton.AccessibleDescription = "Clear geometry visualizations";
        _startStopToggle.AccessibleName = "Start Stop Toggle";
        _startStopToggle.AccessibleDescription = "Toggle trace listener on or off";
        _logTextBox.AccessibleName = "Log Output";
        _logTextBox.AccessibleDescription = "Displays log entries with color coding";
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // Keyboard shortcuts for common actions
        if (e.Control)
        {
            switch (e.KeyCode)
            {
                case Keys.L:
                    Clear();
                    e.Handled = true;
                    break;
                case Keys.G:
                    ClearGeometry();
                    e.Handled = true;
                    break;
                case Keys.Space:
                    IsStarted = !IsStarted;
                    e.Handled = true;
                    break;
            }
        }
    }

    private void SetupDesignTimeData()
    {
        // Add some sample data for design-time preview
        _logTextBox.Text = "Design-time preview:\n[12:34:56 INF] Sample log entry\n[12:34:57 WRN] Sample warning\n[12:34:58 ERR] Sample error";
        _logLevelComboBox.SelectedItem = LogEventLevel.Debug;
        _startStopToggle.Checked = true;

        // Set up tooltips for design-time
        SetupTooltips();
    }

    private void SetupTooltips()
    {
        var toolTip = new ToolTip
        {
            AutoPopDelay = 5000,
            InitialDelay = 1000,
            ReshowDelay = 500,
            ShowAlways = true
        };

        toolTip.SetToolTip(_logLevelComboBox, "Select the minimum log level to display");
        toolTip.SetToolTip(_clearLogButton, "Clear all log entries from the display");
        toolTip.SetToolTip(_clearGeometryButton, "Clear all geometry visualizations from the Revit view");
        toolTip.SetToolTip(_startStopToggle, "Start or stop the trace listener for capturing log events");
        toolTip.SetToolTip(_logTextBox, "Log output display with color-coded entries");
    }

    private void OnLogLevelChanged(object? sender, EventArgs e)
    {
        if (_logLevelComboBox.SelectedItem is LogEventLevel level)
        {
            LogLevel = level;
        }
    }

    private void OnClearLogClick(object? sender, EventArgs e)
    {
        Clear();
    }

    private static void OnClearGeometryClick(object? sender, EventArgs e)
    {
        ClearGeometry();
    }

    private void OnStartStopToggleChanged(object? sender, EventArgs e)
    {
        IsStarted = _startStopToggle.Checked;
    }

    private void OnLogLevelChanged(LogEventLevel value)
    {
        _levelSwitch.MinimumLevel = value;
    }

    private void OnIsStartedChanged(bool value)
    {
        TraceStatus(value);
        _logTextBox.Clear();
        _startStopToggle.Checked = value;

        // Update toggle button text and appearance
        _startStopToggle.Text = value ? "⏸️ Stop Listener" : "▶️ Start Listener";
        // _startStopToggle.BackColor = value
        //     ? System.Drawing.Color.FromArgb(220, 53, 69)   // Red when active
        //     : System.Drawing.Color.FromArgb(40, 167, 69);  // Green when inactive
    }

    private void TraceStatus(bool isStarted)
    {
        if (isStarted)
        {
            Initialized();
            if (_traceListener != null) Trace.Listeners.Add(_traceListener);
            Trace.Listeners.Add(TraceGeometry.TraceListener);
            VisualizationController.Start();
        }
        else
        {
            if (_traceListener != null) Trace.Listeners.Remove(_traceListener);
            Trace.Listeners.Remove(TraceGeometry.TraceListener);
            VisualizationController.Stop();
            CloseAndFlush();
        }
    }

    private void Initialized()
    {
        var options = new RichTextBoxSinkOptions(
            theme: ThemePresets.Colored,
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:l}{NewLine}{Exception}",
            formatProvider: CultureInfo.InvariantCulture);
        
        _sink = new RichTextBoxSink(_logTextBox, options);
        
        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(_levelSwitch)
            .WriteTo.RichTextBox(_logTextBox,
                out _sink,
                formatProvider: CultureInfo.InvariantCulture,
                theme: IsDarkTheme ? AdaptiveThemePresets.EnhancedDark : AdaptiveThemePresets.EnhancedLight,
                autoScroll: true);

        _logger ??= loggerConfig.CreateLogger();
        _traceListener ??= new SerilogTraceListener(_logger);
    }

    private void CloseAndFlush()
    {
        _logger?.Dispose();
        _logger = null;
        _traceListener?.Dispose();
        _traceListener = null;
    }

    private static void InitializeTheme()
    {
#if REVIT2024_OR_GREATER
        // Get current Revit theme and apply it
        var revitTheme = GetRevitTheme();
        ApplicationThemeManager.Apply(revitTheme);
#else
        // For older Revit versions, default to system theme
        ApplicationThemeManager.ApplySystemTheme();
#endif
    }

#if REVIT2024_OR_GREATER
    private static RevitDevTool.Theme.ApplicationTheme GetRevitTheme()
    {
        return UIThemeManager.CurrentTheme switch
        {
            UITheme.Light => RevitDevTool.Theme.ApplicationTheme.Light,
            UITheme.Dark => RevitDevTool.Theme.ApplicationTheme.Dark,
            _ => RevitDevTool.Theme.ApplicationTheme.Light
        };
    }

    private void ApplyRevitTheme(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(UIFramework.ApplicationTheme.CurrentTheme.RibbonPanelBackgroundBrush)) return;
        if (UIThemeManager.CurrentTheme.ToString() == UIFramework.ApplicationTheme.CurrentTheme.RibbonTheme.Name) return;

        // Apply the new Revit theme
        var revitTheme = GetRevitTheme();
        ApplicationThemeManager.Apply(revitTheme);
    }
#endif

    private void OnThemeChanged(ApplicationTheme theme, System.Windows.Media.Color accent)
    {
        if (InvokeRequired)
        {
            Invoke(ApplyTheme);
        }
        else
        {
            ApplyTheme();
        }

        if (IsStarted)
        {
            RestartLogging();
        }
    }

    private void RestartLogging()
    {
        CloseAndFlush();
        ApplyTheme();
        TraceStatus(IsStarted);
    }

    private void ApplyTheme()
    {
        var isDark = IsDarkTheme;

        // Define professional color scheme
        var backgroundColor = isDark
            ? System.Drawing.Color.FromArgb(32, 32, 32)    // Dark background
            : System.Drawing.Color.FromArgb(248, 249, 250); // Light background

        var foregroundColor = isDark
            ? System.Drawing.Color.FromArgb(241, 241, 241)  // Light text on dark
            : System.Drawing.Color.FromArgb(33, 37, 41);    // Dark text on light

        var borderColor = isDark
            ? System.Drawing.Color.FromArgb(64, 64, 64)     // Dark border
            : System.Drawing.Color.FromArgb(206, 212, 218); // Light border

        var buttonBackColor = isDark
            ? System.Drawing.Color.FromArgb(48, 48, 48)     // Dark button background
            : System.Drawing.Color.FromArgb(255, 255, 255); // Light button background

        var logBackColor = isDark
            ? System.Drawing.Color.FromArgb(24, 24, 24)     // Darker log area
            : System.Drawing.Color.FromArgb(255, 255, 255); // White log area

        // Apply theme to main control
        BackColor = backgroundColor;
        ForeColor = foregroundColor;

        // Apply theme to panels with consistent styling
        _mainPanel.BackColor = backgroundColor;
        _topPanel.BackColor = backgroundColor;
        _buttonPanel.BackColor = backgroundColor;

        // Style the log panel with border
        _logPanel.BackColor = borderColor; // Border color shows through padding

        // Apply theme to log text box with enhanced styling
        _logTextBox.BackColor = logBackColor;
        _logTextBox.ForeColor = foregroundColor;

        // Style standard buttons with enhanced appearance
        ApplyButtonTheme(_clearLogButton, buttonBackColor, foregroundColor, borderColor);
        ApplyButtonTheme(_clearGeometryButton, buttonBackColor, foregroundColor, borderColor);
        ApplyCheckBoxTheme(_startStopToggle, buttonBackColor, foregroundColor, borderColor);

        // Style standard ComboBox with enhanced theme support
        _logLevelComboBox.BackColor = buttonBackColor;
        _logLevelComboBox.ForeColor = foregroundColor;
        _logLevelComboBox.FlatStyle = FlatStyle.Standard;

        // Style standard log panel
        _logPanel.BackColor = backgroundColor;

        // Apply Windows dark mode for native controls
        Win32DarkMode.SetImmersiveDarkMode(_logTextBox.Handle, isDark);
        if (_logLevelComboBox.Handle != IntPtr.Zero)
            Win32DarkMode.SetImmersiveDarkMode(_logLevelComboBox.Handle, isDark);
    }

    private static void ApplyButtonTheme(WinFormsButton button, System.Drawing.Color backColor, System.Drawing.Color foreColor, System.Drawing.Color borderColor)
    {
        button.BackColor = backColor;
        button.ForeColor = foreColor;
        button.FlatStyle = FlatStyle.Standard;
        button.FlatAppearance.BorderColor = borderColor;
        button.FlatAppearance.BorderSize = 0;
        button.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(
            Math.Min(255, backColor.R + 20),
            Math.Min(255, backColor.G + 20),
            Math.Min(255, backColor.B + 20));
        button.FlatAppearance.MouseDownBackColor = System.Drawing.Color.FromArgb(
            Math.Max(0, backColor.R - 20),
            Math.Max(0, backColor.G - 20),
            Math.Max(0, backColor.B - 20));

        // Set button text with icons based on button name
        switch (button.Name)
        {
            case "_clearLogButton":
                button.Text = "🗑️ Clear Log";
                break;
            case "_clearGeometryButton":
                button.Text = "🔷 Clear Geometry";
                break;
        }
    }

    private static void ApplyCheckBoxTheme(WinFormsCheckBox checkBox, System.Drawing.Color backColor, System.Drawing.Color foreColor, System.Drawing.Color borderColor)
    {
        checkBox.BackColor = backColor;
        checkBox.ForeColor = foreColor;
        checkBox.FlatStyle = FlatStyle.Standard;
        checkBox.FlatAppearance.BorderColor = borderColor;
        checkBox.FlatAppearance.BorderSize = 0;
        checkBox.FlatAppearance.CheckedBackColor = System.Drawing.Color.FromArgb(
            Math.Max(0, backColor.R - 30),
            Math.Max(0, backColor.G - 30),
            Math.Max(0, backColor.B - 30));
        checkBox.FlatAppearance.MouseOverBackColor = System.Drawing.Color.FromArgb(
            Math.Min(255, backColor.R + 20),
            Math.Min(255, backColor.G + 20),
            Math.Min(255, backColor.B + 20));
    }

#if REVIT2024_OR_GREATER
    private void ApplyTheme(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(UIFramework.ApplicationTheme.CurrentTheme.RibbonPanelBackgroundBrush)) return;
        if (UIThemeManager.CurrentTheme.ToString() == UIFramework.ApplicationTheme.CurrentTheme.RibbonTheme.Name) return;
        ApplyTheme();
    }
#endif

    private void Clear()
    {
        _logTextBox.Clear();
        _sink?.Clear();
    }
    
    private static void ClearGeometry()
    {
        VisualizationController.Clear();
    }

    protected virtual void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void DisposeCustomResources()
    {
#if REVIT2024_OR_GREATER
        try
        {
            UIFramework.ApplicationTheme.CurrentTheme.PropertyChanged -= ApplyRevitTheme;
        }
        catch
        {
            // Ignore
        }
#endif
        ApplicationThemeManager.Changed -= OnThemeChanged;

        if (_traceListener != null)
        {
            Trace.Listeners.Remove(_traceListener);
            Trace.Listeners.Remove(TraceGeometry.TraceListener);
        }

        _logger?.Dispose();
        _logger = null;
        _traceListener?.Dispose();
        _traceListener = null;

        _consoleRedirector.Dispose();
    }
}

internal static class Win32DarkMode
{
    // ReSharper disable InconsistentNaming
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

    [System.Runtime.InteropServices.DllImport("dwmapi.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    [System.Runtime.InteropServices.DllImport("uxtheme.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode, SetLastError = true)]
    private static extern int SetWindowTheme(IntPtr hWnd, string? pszSubAppName, string? pszSubIdList);

    public static void SetImmersiveDarkMode(IntPtr hwnd, bool enable)
    {
        if (hwnd == IntPtr.Zero) return;
        var useDark = enable ? 1 : 0;
        _ = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
        _ = DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref useDark, sizeof(int));
        try
        {
            SetWindowTheme(hwnd, enable ? "DarkMode_Explorer" : "Explorer", null);
        }
        catch
        {
            // ignore
        }
    }
}
