using System.Drawing;
using System.Reflection;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RevitDevTool.Scintilla.Control;
using RevitDevTool.Scintilla.Core;
using RevitDevTool.Scintilla.Extensions;
using RevitDevTool.Scintilla.Formatting;
using RevitDevTool.Scintilla.Render;
using ThemeStyle = RevitDevTool.Scintilla.Core.Style;

namespace RevitDevTool.Scintilla.Wpf.Demo;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<DemoEnrichmentCallbacks>();
        builder.Services.AddScintillaLogViewerWpf(sp => new ScintillaLogViewerOptions
        {
            ChannelCapacity = 50_000,
            MaxLines = 50_000,
            MaxHistoryEntries = 50_000,
            MaxBatchSize = 800,
            FlushIntervalMs = 50,
            TrimChunkLines = 5_000,
            Theme = ThemePresets.EnhancedDark.WithCustomStyle(
                StyleToken.TokenClassified,
                new ThemeStyle(Color.FromArgb(120, 255, 170), Color.FromArgb(37, 37, 37), bold: true)),
            EnableTokenLinks = true,
            EnableTokenHighlight = true,
            EnablePrettyJson = true,
            EnrichmentCallbacks = sp.GetRequiredService<DemoEnrichmentCallbacks>(),
            TokenClassifier = new DemoRevitTokenClassifier(),
            TokenLinkClicked = token =>
            {
                if (token is DemoTokenPayload payload)
                {
                    System.Diagnostics.Debug.WriteLine($"WPF token clicked: {payload.Kind} => {payload.TargetUri}");
                    return;
                }

                if (TryGetHttpTargetUri(token, out var targetUri))
                    TryOpenExternalUri(targetUri);
            }
        });
        builder.Services.AddSingleton<MainWindow>();
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Trace);
        builder.Logging.AddZLoggerScintilla(zlogger =>
        {
            zlogger.IncludeScopes = true;
            zlogger.UsePlainTextFormatter(formatter =>
            {
                formatter.SetPrefixFormatter(
                    $"[{0:local-timeonly} {1:short}] ",
                    (in template, in info) => template.Format(info.Timestamp, info.LogLevel));
            });
        });

        _host = builder.Build();
        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        _host = null;
        base.OnExit(e);
    }

    private static bool TryGetHttpTargetUri(ILogTokenPayload payload, out string targetUri)
    {
        targetUri = string.Empty;
        var property = payload.GetType().GetProperty("TargetUri", BindingFlags.Instance | BindingFlags.Public);
        if (property?.GetValue(payload) is not string raw || string.IsNullOrWhiteSpace(raw))
            return false;

        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
            return false;

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        targetUri = uri.ToString();
        return true;
    }

    private static void TryOpenExternalUri(string targetUri)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = targetUri,
                UseShellExecute = true
            });
        }
        catch
        {
            // Ignore demo-only browser launch errors.
        }
    }
}
