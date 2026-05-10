using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace RevitDevTool.ExternalEvent.HotReloadUi;

/// <summary>
///     WPF root loaded only inside the host's collectible ALC (separate assembly from the repacked add-in).
/// </summary>
public partial class HotReloadPanePage : UserControl
{
    internal const string HostAssemblySimpleName = "RevitDevTool.ExternalEvent.App";

    private const string CollectibleAssemblyPaneHostFullName = "RevitDevTool.Core.CollectibleAssemblyPaneHost";
    private const string PaneReloadPropertyName = "PaneReloadFromDisk";

    public HotReloadPanePage()
    {
        InitializeComponent();
        InstanceLabel.Text =
            $"instance id: {Guid.NewGuid():N}\nloaded (UTC): {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}";
    }

    private void OnReloadClick(object sender, RoutedEventArgs e)
    {
        TryInvokePaneReloadFromDisk();
    }

    /// <summary>
    ///     Calls <c>CollectibleAssemblyPaneHost.PaneReloadFromDisk</c> on the default-loaded host assembly.
    /// </summary>
    internal static void TryInvokePaneReloadFromDisk()
    {
        var qualified = $"{CollectibleAssemblyPaneHostFullName}, {HostAssemblySimpleName}";
        var hostType = Type.GetType(qualified, throwOnError: false);
        if (hostType is null)
            return;

        var prop = hostType.GetProperty(PaneReloadPropertyName, BindingFlags.Public | BindingFlags.Static);
        if (prop?.GetValue(null) is not Action callback)
            return;

        callback.Invoke();
    }
}
