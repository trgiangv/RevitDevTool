using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using DevTools.Daemon.Desktop;

namespace DevTools.Daemon.Views;

public sealed class HostsView(HostInstances hosts) : UserControl
{
    protected override Element OnBuild()
    {
        var items = new ItemsView<HostRow>(
            hosts.Rows,
            textSelector: host => host.Host,
            keySelector: host => host.Pid);

        var table = new GridView()
            .ItemsSource(items)
            .BorderBrush(Color.Transparent)
            .Columns(
                new GridViewColumn<HostRow>()
                    .Header("Host")
                    .StarWidth(minWidth: 120)
                    .Text(row => row.Host),
                new GridViewColumn<HostRow>()
                    .Header("Version")
                    .StarWidth(minWidth: 80)
                    .Text(row => row.Version),
                new GridViewColumn<HostRow>()
                    .Header("PID")
                    .PixelWidth(80)
                    .Text(row => row.Pid.ToString()),
                new GridViewColumn<HostRow>()
                    .Header("Status")
                    .PixelWidth(120)
                    .Text(row => row.Status));

        var empty = new TextBlock()
            .Text("No host instances discovered.")
            .Center()
            .IsHitTestVisible(false)
            .WithTheme((theme, block) => block.Foreground(theme.Palette.PlaceholderText))
            .BindIsVisible(hosts.Count, count => count == 0);

        return new Grid()
            .Children(table, empty);
    }
}
