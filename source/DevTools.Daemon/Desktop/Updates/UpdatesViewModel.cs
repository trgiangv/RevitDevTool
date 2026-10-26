using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DevTools.Hosting;
using DevTools.Update.Apply;
using DevTools.Update.Autodesk;
using DevTools.Update.Engine;
using DevTools.Update.Installations;
using System.Collections.ObjectModel;

namespace DevTools.Daemon.Desktop.Updates;

/// <summary>
///     Updates tab of the main window. It shows the host selection (0047) and the update mode (0050).
/// </summary>
public sealed partial class UpdatesViewModel(UpdateEngine engine) : ObservableObject
{
    public ObservableCollection<TargetRow> Rows { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ApplyCommand), nameof(CheckForUpdatesCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool AutoUpdate { get; set; } = true;

    [ObservableProperty]
    public partial bool IncludePrereleases { get; set; }

    [ObservableProperty]
    public partial string Status { get; set; } = "";

    private Task _load = Task.CompletedTask;

    public Task LoadAsync() => _load = RunAsync(async () =>
    {
        await LoadRowsAsync().ConfigureAwait(true);

        var policy = engine.Policy;
        AutoUpdate = policy.Mode != UpdateMode.Notify;
        IncludePrereleases = policy.IsPrerelease;
        Status = Rows.Count == 0 ? "No release with update data is available."
            : engine.Store.Exists ? ""
            : "Select the hosts, then select Apply.";
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
        Status = result.Status == UpdateCycleStatus.NotConfigured ? "Select the hosts, then select Apply." : "";
        await LoadRowsAsync().ConfigureAwait(true);
    });

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private Task ApplyAsync() => RunAsync(async () =>
    {
        Status = "";
        var selection = Rows.Where(row => row.IsSelected).Select(row => row.Component.Target).ToArray();
        var progress = new Progress<PayloadProgress>(report =>
        {
            foreach (var row in Rows.Where(row => row.Component.Target.HostVersion == report.HostVersion)) row.Show(report);
        });
        await engine.ApplySelectionAsync(selection, ChosenPolicy(), null, progress, CancellationToken.None).ConfigureAwait(true);
        await LoadRowsAsync().ConfigureAwait(true);
    });

    private async Task LoadRowsAsync()
    {
        var options = await engine.GetTargetsAsync(CancellationToken.None).ConfigureAwait(true);
        Rows.Clear();
        foreach (var option in options) Rows.Add(new TargetRow(option) { IsSelected = option.Selected });
    }

    private bool IsIdle() => !IsBusy;

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
    public bool OnComputer { get; } = option.OnComputer;
    public string HostTip { get; } = option.OnComputer
        ? $"On this computer: {string.Join(", ", option.Hosts.Select(host => $"{ProductName(host)} {option.Component.Target.HostVersion}"))}"
        : "Not found on this computer";
    public string Installed { get; } = option.Installed ?? "—";
    public bool IsPending { get; } = option.Pending is not null;
    public string InstalledTip { get; } = option.Pending is { } pending ? $"{pending} is installed when the host closes" : "Installed version";
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
        ProgressTip = progress.Stage == PayloadStage.Downloading ? $"Downloading {Percent:0}%" : "Installing";
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
