using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

    [ObservableProperty]
    public partial string Installed { get; set; } = "";

    private Task _load = Task.CompletedTask;

    public Task LoadAsync() => _load = RunAsync(async () =>
    {
        var options = await engine.GetTargetsAsync(CancellationToken.None).ConfigureAwait(true);
        Rows.Clear();
        foreach (var option in options) Rows.Add(new TargetRow(option.Component, option.OnComputer) { IsSelected = option.Selected });

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
        Status = result.Status switch
        {
            UpdateCycleStatus.Notified => $"Version {result.Version} is available.",
            UpdateCycleStatus.Updated => $"Version {result.Version} is installed.",
            UpdateCycleStatus.NotConfigured => "Select the hosts, then select Apply.",
            _ => "No update is available."
        };
    });

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private Task ApplyAsync() => RunAsync(async () =>
    {
        var selection = Rows.Where(row => row.IsSelected).Select(row => row.Component.Target).ToArray();
        var result = await engine.ApplySelectionAsync(selection, ChosenPolicy(), null, CancellationToken.None).ConfigureAwait(true);
        Status = result.Pending.Count > 0
            ? $"Version {result.Version} is installed. {string.Join(", ", result.Pending)} completes when the host closes."
            : $"Version {result.Version} is installed.";
    });

    private bool IsIdle() => !IsBusy;

    partial void OnAutoUpdateChanged(bool value) => SavePolicy();

    partial void OnIncludePrereleasesChanged(bool value) => SavePolicy();

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
            Installed = Describe(engine.Store.Load());
            IsBusy = false;
        }
    }

    private static string Describe(Installation? installation) => installation is null || installation.Payloads.Count == 0
        ? "Nothing is installed."
        : string.Join("   ", installation.Payloads.Select(payload => payload.PendingVersion is { } pending
            ? $"{payload.HostVersion}: {payload.Version} → {pending} (waits)"
            : $"{payload.HostVersion}: {payload.Version}"));
}

public sealed partial class TargetRow(AddinComponent component, bool onComputer) : ObservableObject
{
    public AddinComponent Component { get; } = component;
    public string Detail { get; } = onComputer ? "Installed on this computer" : "Not found on this computer";

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}
