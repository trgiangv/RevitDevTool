using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTools.Hosting;
using DevTools.Update.Apply;
using DevTools.Update.Autodesk;
using DevTools.Update.Engine;
using DevTools.Update.Installations;
using System.Collections.ObjectModel;
// ReSharper disable UnusedParameterInPartialMethod

namespace DevTools.Daemon.Desktop.Updates;

/// <summary>
///     Updates tab of the main window. It shows the host selection (0047) and the update mode (0050).
/// </summary>
public sealed partial class UpdatesViewModel(UpdateEngine engine) : ObservableObject
{
    public ObservableCollection<TargetRow> Rows { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand), nameof(CheckForUpdatesCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand))]
    public partial bool IsInstalling { get; set; }

    [ObservableProperty]
    public partial double Progress { get; set; }

    [ObservableProperty]
    public partial string ProgressText { get; set; } = "";

    [ObservableProperty]
    public partial bool AutoUpdate { get; set; } = true;

    [ObservableProperty]
    public partial bool IncludePrereleases { get; set; }

    [ObservableProperty]
    public partial string Status { get; set; } = "";

    private const string SelectPrompt = "Select the hosts, then select Install.";

    private Task _load = Task.CompletedTask;

    public Task LoadAsync() => _load = RunAsync(async () =>
    {
        await LoadRowsAsync().ConfigureAwait(true);

        var policy = engine.Policy;
        AutoUpdate = policy.Mode != UpdateMode.Notify;
        IncludePrereleases = policy.IsPrerelease;
        Status = Rows.Count == 0 ? "No release with update data is available."
            : engine.Store.Exists ? ""
            : SelectPrompt;
    });

    /// <summary>
    ///     Checks for updates after the tab load that is in progress.
    /// </summary>
    public async Task CheckAfterLoadAsync()
    {
        await _load.ConfigureAwait(true);
        await CheckForUpdatesCommand.ExecuteAsync(null).ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private Task CheckForUpdatesAsync() => RunAsync(async () =>
    {
        var result = await engine.CheckAsync(UpdateTrigger.Manual, CancellationToken.None).ConfigureAwait(true);
        Status = result.Status == UpdateCycleStatus.NotConfigured ? SelectPrompt : "";
        await LoadRowsAsync().ConfigureAwait(true);
    });

    [RelayCommand(CanExecute = nameof(CanInstall), AllowConcurrentExecutions = true)]
    private Task InstallAsync() => RunAsync(async () =>
    {
        Status = "";
        Progress = 0;
        ProgressText = "Installing";
        IsInstalling = true;
        try
        {
            var selection = Rows.Where(row => row.IsSelected).Select(row => row.Component.Target).ToArray();
            await engine.ApplySelectionAsync(selection, ChosenPolicy(), null, TrackProgress(), CancellationToken.None).ConfigureAwait(true);
        }
        finally
        {
            IsInstalling = false;
        }

        await LoadRowsAsync().ConfigureAwait(true);
    });

    private Progress<PayloadProgress> TrackProgress()
    {
        var payloads = new Dictionary<string, double>(StringComparer.Ordinal);
        return new Progress<PayloadProgress>(report =>
        {
            foreach (var row in Rows.Where(row => row.Component.Target.HostVersion == report.HostVersion)) row.Show(report);

            payloads[report.HostVersion] = report.Stage == PayloadStage.Installing ? 100 : report.Percent ?? 0;
            Progress = payloads.Values.Average();
            ProgressText = Progress < 100 ? $"Downloading {Progress:0}%" : "Installing";
        });
    }

    private async Task LoadRowsAsync()
    {
        var options = await engine.GetTargetsAsync(CancellationToken.None).ConfigureAwait(true);
        Rows.Clear();
        foreach (var option in options)
        {
            var row = new TargetRow(option);
            row.IsSelected = option.Selected && row.CanSelect;
            Rows.Add(row);
        }
    }

    private bool IsIdle() => !IsBusy;

    private bool CanInstall() => !IsBusy || IsInstalling;

    partial void OnAutoUpdateChanged(bool value) => SavePolicy();

    partial void OnIncludePrereleasesChanged(bool value)
    {
        SavePolicy();
        if (!IsBusy) LoadAsync();
    }

    private UpdatePolicy ChosenPolicy()
    {
        var policy = engine.Policy;
        policy.Mode = AutoUpdate ? UpdateMode.Auto : UpdateMode.Notify;
        policy.Channel = IncludePrereleases ? UpdatePolicy.PrereleaseChannel : UpdatePolicy.StableChannel;
        return policy;
    }

    private void SavePolicy()
    {
        var saved = engine.Policy;
        var chosen = ChosenPolicy();
        if (saved.Mode != chosen.Mode || saved.Channel != chosen.Channel) engine.SetPolicy(chosen);
    }

    private async Task RunAsync(Func<Task> action)
    {
        IsBusy = true;
        try
        {
            await action().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            Status = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public sealed partial class TargetRow(TargetOption option) : ObservableObject
{
    public AddinComponent Component { get; } = option.Component;
    public string Description { get; } = option.OnComputer ? option.Component.Description : $"{option.Component.Description} <Not installed>";
    public bool CanSelect { get; } = option.OnComputer || option.Installed is not null;
    public string? HostTip { get; } = option.OnComputer
        ? string.Join(", ", option.Hosts.Select(host => $"{ProductName(host)} {option.Component.Target.HostVersion}"))
        : null;
    public string? Installed { get; } = option.Installed;
    public bool IsPending { get; } = option.Pending is not null;
    public string? PendingTip { get; } = option.Pending is { } pending ? $"{pending} is installed when the host closes" : null;
    public string Latest { get; } = option.Latest;
    public bool HasUpdate { get; } = (option.Pending ?? option.Installed) is { } version && version != option.Latest;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    [ObservableProperty]
    public partial bool IsWorking { get; set; }

    [ObservableProperty]
    public partial bool IsIndeterminate { get; set; }

    [ObservableProperty]
    public partial double Percent { get; set; }

    [ObservableProperty]
    public partial string ProgressTip { get; set; } = "";

    public void Show(PayloadProgress progress)
    {
        IsWorking = true;
        IsIndeterminate = progress.Percent is null;
        Percent = progress.Percent ?? 0;
        ProgressTip = progress.Stage switch
        {
            PayloadStage.Waiting => "Waiting",
            PayloadStage.Downloading => $"Downloading {Percent:0}%",
            _ => "Installing"
        };
    }

    private static string ProductName(HostApp host) => host switch
    {
        HostApp.AutoCad => "AutoCAD",
        HostApp.Civil3D => "Civil 3D",
        HostApp.Plant3D => "Plant 3D",
        HostApp.AcadArch => "AutoCAD Architecture",
        HostApp.AcadMech => "AutoCAD Mechanical",
        HostApp.AcadElec => "AutoCAD Electrical",
        HostApp.AcadMep => "AutoCAD MEP",
        HostApp.AcadMap3D => "AutoCAD Map 3D",
        _ => host.ToString()
    };
}
