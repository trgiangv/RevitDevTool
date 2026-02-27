using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RevitDevTool.Scintilla;
using RevitDevTool.Scintilla.Contracts;
using RevitDevTool.Scintilla.Logging;
using ZLogger;

namespace RevitDevTool.Scintilla.Demo;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddSingleton(new ScintillaLogViewerHost(new ScintillaLogViewerOptions
        {
            ChannelCapacity = 50_000,
            MaxLines = 50_000,
            MaxHistoryEntries = 50_000,
            MaxBatchSize = 800,
            FlushIntervalMs = 50,
            TrimChunkLines = 5_000
        }));
        builder.Services.AddSingleton<MainForm>();

        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Trace);
        builder.Logging.AddZLoggerScintilla(
            sp => sp.GetRequiredService<ScintillaLogViewerHost>().Controller,
            options =>
            {
                options.MinimumLevel = LogLevel.Trace;
            });
        builder.Logging.AddZLoggerRollingFile(
            (date, index) => Path.Combine(Path.GetTempPath(), $"scintilla-zlogger-demo-{date:yyyyMMdd}-{index}.log"),
            ZLogger.Providers.RollingInterval.Day);

        using var host = builder.Build();
        var mainForm = host.Services.GetRequiredService<MainForm>();
        Application.Run(mainForm);
    }
}
