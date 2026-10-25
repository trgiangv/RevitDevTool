using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTools.Daemon.Desktop.Helpers;
using DevTools.Update.Engine;
using DevTools.Utilities;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.IO;
using System.Windows;
using ZLogger;

namespace DevTools.Daemon.Desktop.Updates;

/// <summary>
///     Tray part of the update engine (0050). It shows notices as balloons and gives menu commands for their actions.
/// </summary>
public sealed partial class UpdateTray : ObservableObject
{
    private readonly UpdateEngine _engine;
    private readonly UpdateScheduler _scheduler;
    private readonly ILogger<UpdateTray> _logger;

    public UpdateTray(UpdateEngine engine, UpdateScheduler scheduler, UpdateNotices notices, ILogger<UpdateTray> logger)
    {
        _engine = engine;
        _scheduler = scheduler;
        _logger = logger;
        notices.Raised += Notify;
        notices.ShutdownRequested += Shutdown;
    }

    public event Action<UpdateNotice>? NoticeRaised;

    /// <summary>
    ///     Opens the main window on the Updates tab. The argument is true if the tab must also check for updates.
    /// </summary>
    public event Action<bool>? ShowUpdatesRequested;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAction), nameof(ActionHeader))]
    public partial UpdateNotice? Actionable { get; set; }

    public bool HasAction => Actionable is not null;

    public string ActionHeader => Actionable?.Kind switch
    {
        UpdateNoticeKind.Available => $"Install update {Actionable.Version}",
        UpdateNoticeKind.DaemonPending => $"Restart now to install {Actionable.Version}",
        UpdateNoticeKind.NewTarget => "Add the new hosts…",
        UpdateNoticeKind.Failed => "Open the update log",
        _ => ""
    };

    private void Notify(UpdateNotice notice)
    {
        _logger.ZLogInformation($"Update notice {notice.Kind}: {notice.Message}");
        UiDispatchHelper.Post(() =>
        {
            if (notice.Kind is UpdateNoticeKind.Available or UpdateNoticeKind.DaemonPending or UpdateNoticeKind.NewTarget or UpdateNoticeKind.Failed)
                Actionable = notice;
            else if (notice.Kind == UpdateNoticeKind.Applied && Actionable?.Kind == UpdateNoticeKind.Available)
                Actionable = null;

            NoticeRaised?.Invoke(notice);
        });
    }

    private void Shutdown()
    {
        _logger.ZLogInformation($"The Daemon stops. Velopack applies the update and starts it again");
        UiDispatchHelper.Post(() => Application.Current.Shutdown());
    }

    /// <summary>
    ///     Opens the Updates tab on the first run, or if the install has no saved selection.
    /// </summary>
    public void ShowSetupIfNotConfigured(DaemonUpdater daemon)
    {
        if (daemon.IsInstalled && !_engine.Store.Exists) ChooseHosts();
    }

    [RelayCommand]
    private void CheckForUpdates() => UiDispatchHelper.Post(() => ShowUpdatesRequested?.Invoke(true));

    private void ChooseHosts() => UiDispatchHelper.Post(() => ShowUpdatesRequested?.Invoke(false));

    [RelayCommand]
    private void RunAction()
    {
        var kind = Actionable?.Kind;
        if (kind != UpdateNoticeKind.DaemonPending) Actionable = null;

        switch (kind)
        {
            case UpdateNoticeKind.Available:
                _scheduler.Trigger(UpdateTrigger.UserApproved);
                break;
            case UpdateNoticeKind.DaemonPending:
                _engine.ApplyDaemonNow();
                break;
            case UpdateNoticeKind.NewTarget:
                ChooseHosts();
                break;
            case UpdateNoticeKind.Failed:
                Open(Path.Combine(AppUtils.GetApplicationDataPath(), "logs"));
                break;
        }
    }

    private static void Open(string target) => Process.Start(new ProcessStartInfo(target) { UseShellExecute = true })?.Dispose();
}
