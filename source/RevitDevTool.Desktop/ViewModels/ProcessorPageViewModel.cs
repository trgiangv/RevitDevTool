using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;
using RevitDevTool.Bridge;
using RevitDevTool.Bridge.Enums;
using RevitDevTool.Bridge.Enums.Revit;
using RevitDevTool.Console.Services.Hosting;
using RevitDevTool.Desktop.Models;
using RevitDevTool.Desktop.Services;

namespace RevitDevTool.Desktop.ViewModels;

public partial class ProcessorPageViewModel : PageViewModelBase
{
    public override int Index => 0;
    public override string DisplayName => "Processor";
    public override MaterialIconKind Icon => MaterialIconKind.CogPlay;

    private readonly IBatchExecutionService _batchExecutionService;
    private CancellationTokenSource? _runCts;
    private bool _updatingSelectAllQueueItems;
    private System.Timers.Timer? _systemMonitorTimer;
    private Process? _currentProcess;

    [ObservableProperty] private string _configPath = string.Empty;
    [ObservableProperty] private bool _forceLaunch;
    [ObservableProperty] private int _parallelCount = 2;
    [ObservableProperty] private ProcessingMode _selectedMode = ProcessingMode.SequentialMulti;
    [ObservableProperty] private string _selectedRevitVersion = "2025";
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _status = "Idle";
    [ObservableProperty] private string _runSummary = "No run yet.";
    [ObservableProperty] private int _totalFiles;
    [ObservableProperty] private int _successCount;
    [ObservableProperty] private int _failureCount;
    [ObservableProperty] private long _totalDurationMs;
    [ObservableProperty] private string _crashRiskMessage = string.Empty;
    [ObservableProperty] private bool _hasCrashRisk;
    [ObservableProperty] private string _queueSearchText = string.Empty;
    [ObservableProperty] private string _queueStatusFilter = "All";
    [ObservableProperty] private bool _selectAllQueueItems;
    [ObservableProperty] private int _selectedQueueCount;
    [ObservableProperty] private string _configValidationMessage = string.Empty;
    [ObservableProperty] private string _executionValidationMessage = string.Empty;
    [ObservableProperty] private bool _suppressDialogs;
    [ObservableProperty] private bool _dryRun = true;
    [ObservableProperty] private bool _autoScrollLogs = true;
    [ObservableProperty] private double _overallProgressPercent;

    // System Health - CPU/RAM
    [ObservableProperty] private double _cpuUsagePercent;
    [ObservableProperty] private double _ramUsagePercent;
    [ObservableProperty] private string _cpuUsageText = "0%";
    [ObservableProperty] private string _ramUsageText = "0%";

    // New execution logic fields
    [ObservableProperty] private bool _isHeadlessMode = true;
    [ObservableProperty] private bool _audit;
    [ObservableProperty] private CentralMode _detachFromCentral = CentralMode.DetachAndPreserveWorksets;
    [ObservableProperty] private WorksetMode _workset = WorksetMode.OpenAllWorksets;
    [ObservableProperty] private bool _openWorksets;
    [ObservableProperty] private bool _closeWorksets;
    [ObservableProperty] private bool _closeDocument = true;
    [ObservableProperty] private bool _closeHost;

    public IReadOnlyList<string> RevitVersions { get; }
    public IReadOnlyList<CentralMode> DetachOptions { get; } = Enum.GetValues<CentralMode>();
    public IReadOnlyList<WorksetMode> WorksetOptions { get; } = Enum.GetValues<WorksetMode>();
    public IReadOnlyList<ProcessingMode> Modes { get; } = Enum.GetValues<ProcessingMode>();
    public ObservableCollection<PlanJobItem> PlanJobs { get; } = [];
    public ObservableCollection<HostProgressItem> ProgressItems { get; } = [];
    public ObservableCollection<HostLogItem> LogItems { get; } = [];
    public ObservableCollection<ResultRowItem> ResultItems { get; } = [];
    public ObservableCollection<HostInstanceItem> ConnectedInstances { get; } = [];
    public ObservableCollection<QueueTaskItem> TaskQueueItems { get; } = [];
    public ObservableCollection<QueueTaskItem> VisibleQueueItems { get; } = [];
    public IReadOnlyList<string> QueueStatusFilters { get; } = ["All", "Queued", "Running", "Completed", "Failed", "Canceled"];
    public bool HasConfigValidation => !string.IsNullOrWhiteSpace(ConfigValidationMessage);
    public bool HasExecutionValidation => !string.IsNullOrWhiteSpace(ExecutionValidationMessage);
    public bool HasVisibleQueueItems => VisibleQueueItems.Count > 0;
    public bool IsQueueEmpty => VisibleQueueItems.Count == 0;
    public bool HasLogItems => LogItems.Count > 0;
    public bool IsLogEmpty => LogItems.Count == 0;

    private ExecutionPlan? _loadedPlan;

    public ProcessorPageViewModel(IBatchExecutionService batchExecutionService)
    {
        _batchExecutionService = batchExecutionService;
        _batchExecutionService.OnProgress += progress => Avalonia.Threading.Dispatcher.UIThread.Post(() => HandleProgress(progress));
        _batchExecutionService.OnHostLog += log => Avalonia.Threading.Dispatcher.UIThread.Post(() => HandleHostLog(log));
        _batchExecutionService.OnDiagnostic += message => Avalonia.Threading.Dispatcher.UIThread.Post(() => HandleDiagnostic(message));
        TaskQueueItems.CollectionChanged += OnQueueCollectionChanged;
        LogItems.CollectionChanged += OnLogItemsCollectionChanged;

        var scanner = new RevitVersionScanner();
        var installed = scanner.GetInstalledVersions();
        RevitVersions = installed.Count > 0 ? installed : ["2024", "2025", "2026"];
        SelectedRevitVersion = RevitVersions.Contains("2025") ? "2025" : RevitVersions[^1];

        RefreshInstances();
        StartSystemMonitoring();
    }

    private void StartSystemMonitoring()
    {
        _systemMonitorTimer = new System.Timers.Timer(1000);
        _systemMonitorTimer.Elapsed += (s, e) => UpdateSystemHealth();
        _systemMonitorTimer.Start();
        UpdateSystemHealth();
    }

    private void UpdateSystemHealth()
    {
        try
        {
            _currentProcess = Process.GetCurrentProcess();
            var cpuUsage = Math.Round(_currentProcess.TotalProcessorTime.TotalMilliseconds / Environment.ProcessorCount / 10, 1);
            CpuUsagePercent = Math.Min(cpuUsage, 100);
            CpuUsageText = $"{CpuUsagePercent:0}%";

            var ramUsage = Math.Round(_currentProcess.WorkingSet64 / (double)(1024 * 1024 * 1024) * 100, 1);
            RamUsagePercent = Math.Min(ramUsage, 100);
            RamUsageText = $"{RamUsagePercent:0}%";
        }
        catch { }
    }

    partial void OnIsRunningChanged(bool value)
    {
        LoadPlanCommand.NotifyCanExecuteChanged();
        RunCommand.NotifyCanExecuteChanged();
        DryRunCommand.NotifyCanExecuteChanged();
        CancelCommand.NotifyCanExecuteChanged();
        RefreshInstancesCommand.NotifyCanExecuteChanged();
        RemoveSelectedCommand.NotifyCanExecuteChanged();
        AssignVersionCommand.NotifyCanExecuteChanged();
        ChangeViewCommand.NotifyCanExecuteChanged();
    }

    partial void OnConfigPathChanged(string value) => ValidateConfigPath(value);

    partial void OnParallelCountChanged(int value)
    {
        ExecutionValidationMessage = value is < 1 or > 12 ? "Parallel count must be between 1 and 12." : string.Empty;
    }

    partial void OnQueueSearchTextChanged(string value) => RebuildQueueView();
    partial void OnQueueStatusFilterChanged(string value) => RebuildQueueView();

    partial void OnSelectAllQueueItemsChanged(bool value)
    {
        if (_updatingSelectAllQueueItems) return;
        foreach (var item in VisibleQueueItems)
            item.IsSelected = value;
        UpdateSelectedQueueCount();
    }

    partial void OnConfigValidationMessageChanged(string value) => OnPropertyChanged(nameof(HasConfigValidation));
    partial void OnExecutionValidationMessageChanged(string value) => OnPropertyChanged(nameof(HasExecutionValidation));

    [RelayCommand(CanExecute = nameof(CanLoadPlan))]
    private async Task LoadPlanAsync()
    {
        if (string.IsNullOrWhiteSpace(ConfigPath))
        {
            Status = "Select a config file first.";
            return;
        }

        try
        {
            Status = "Loading and resolving execution plan...";
            ValidateConfigPath(ConfigPath);
            if (!string.IsNullOrEmpty(ConfigValidationMessage)) return;

            var options = new ProcessorRunOptions
            {
                ProcessingMode = SelectedMode,
                ParallelCount = ParallelCount > 0 ? ParallelCount : null,
                ForceLaunch = ForceLaunch
            };
            _loadedPlan = await _batchExecutionService.LoadPlanAsync(ConfigPath, options);
            PlanJobs.Clear();
            TaskQueueItems.Clear();
            foreach (var job in _loadedPlan.Jobs)
            {
                PlanJobs.Add(new PlanJobItem(job.FilePath, job.HostVersion, job.Open.Headless, job.Lifecycle.CloseDocument, job.Lifecycle.CloseHost));
                TaskQueueItems.Add(QueueTaskItem.FromResolvedJob(job, PlanJobs.Count));
            }
            RebuildQueueView();
            Status = $"Plan ready: {_loadedPlan.Jobs.Count} job(s).";
        }
        catch (Exception ex)
        {
            Status = "Failed to load plan.";
            LogItems.Add(new HostLogItem(DateTimeOffset.Now.ToString("HH:mm:ss"), "Error", "ui", ex.Message, ex.ToString()));
        }
    }

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RunAsync()
    {
        if (_loadedPlan == null)
        {
            await LoadPlanAsync();
            if (_loadedPlan == null) return;
        }

        _runCts = new CancellationTokenSource();
        try
        {
            IsRunning = true;
            Status = "Running...";
            RunSummary = "Execution in progress.";
            CrashRiskMessage = string.Empty;
            HasCrashRisk = false;
            ProgressItems.Clear();
            ResultItems.Clear();
            foreach (var queueItem in TaskQueueItems)
            {
                queueItem.Status = "Running";
                queueItem.DurationMs = 0;
            }
            RebuildQueueView();

            var result = await _batchExecutionService.RunAsync(_loadedPlan, _runCts.Token);
            ApplyResult(result);
            Status = result.FailureCount > 0 ? "Completed with failures" : "Completed";
        }
        catch (OperationCanceledException)
        {
            Status = "Canceled";
            RunSummary = "Run canceled by user.";
        }
        catch (Exception ex)
        {
            Status = "Failed";
            RunSummary = ex.Message;
            LogItems.Add(new HostLogItem(DateTimeOffset.Now.ToString("HH:mm:ss"), "Error", "ui", ex.Message, ex.ToString()));
        }
        finally
        {
            IsRunning = false;
            _runCts?.Dispose();
            _runCts = null;
            RefreshInstances();
        }
    }

    [RelayCommand(CanExecute = nameof(CanDryRun))]
    private async Task DryRunAsync()
    {
        await LoadPlanAsync().ConfigureAwait(false);
        if (_loadedPlan == null) return;
        Status = "Dry run preview ready";
        RunSummary = $"Dry run: {_loadedPlan.Jobs.Count} queued item(s).";
        LogItems.Add(new HostLogItem(DateTimeOffset.Now.ToString("HH:mm:ss"), "Diagnostic", "ui", "[UIOnly] Dry run validates config and queue composition only.", null));
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel() => _runCts?.Cancel();

    [RelayCommand(CanExecute = nameof(CanRefreshInstances))]
    private void RefreshInstances()
    {
        ConnectedInstances.Clear();
        foreach (var instance in _batchExecutionService.DiscoverInstances())
            ConnectedInstances.Add(instance);
    }

    [RelayCommand(CanExecute = nameof(CanQueueBulkActions))]
    private void RemoveSelected()
    {
        var selected = TaskQueueItems.Where(item => item.IsSelected).ToList();
        if (selected.Count == 0) return;
        foreach (var item in selected)
            TaskQueueItems.Remove(item);
        ReindexQueueItems();
        AppendUiDiagnostic($"[UIOnly] Removed {selected.Count} selected queue item(s).");
    }

    [RelayCommand(CanExecute = nameof(CanQueueBulkActions))]
    private void AssignVersion() => AppendUiDiagnostic($"[UIOnly] Assign Version clicked for {TaskQueueItems.Count(item => item.IsSelected)} selected row(s).");

    [RelayCommand(CanExecute = nameof(CanQueueBulkActions))]
    private void ChangeView() => AppendUiDiagnostic($"[UIOnly] Change View clicked for {TaskQueueItems.Count(item => item.IsSelected)} selected row(s).");

    [RelayCommand]
    private void ClearLogs() => LogItems.Clear();

    public void SetConfigPath(string path) => ConfigPath = path;

    private bool CanLoadPlan() => !IsRunning;
    private bool CanRun() => !IsRunning;
    private bool CanDryRun() => !IsRunning;
    private bool CanCancel() => IsRunning;
    private bool CanRefreshInstances() => !IsRunning;
    private bool CanQueueBulkActions() => !IsRunning && SelectedQueueCount > 0;


    private void ApplyResult(BatchResult result)
    {
        TotalFiles = result.TotalFiles;
        SuccessCount = result.SuccessCount;
        FailureCount = result.FailureCount;
        TotalDurationMs = result.TotalDurationMs;
        RunSummary = $"Total {result.TotalFiles}, Success {result.SuccessCount}, Failed {result.FailureCount}, Duration {result.TotalDurationMs}ms";
        OverallProgressPercent = result.TotalFiles == 0 ? 0 : Math.Clamp((result.SuccessCount + result.FailureCount) / (double)result.TotalFiles * 100.0d, 0.0d, 100.0d);

        ResultItems.Clear();
        for (var i = 0; i < result.Results.Count; i++)
        {
            var row = result.Results[i];
            ResultItems.Add(new ResultRowItem(i + 1, row.Success, row.DurationMs, row.Error, row.StackTrace));
            if (i < TaskQueueItems.Count)
            {
                TaskQueueItems[i].Status = row.Success ? "Completed" : "Failed";
                TaskQueueItems[i].DurationMs = row.DurationMs;
            }
        }
        RebuildQueueView();
    }

    private void HandleProgress(HostProgressItem progress)
    {
        ProgressItems.Add(progress);
        Status = $"Running ({progress.HostLabel})";
    }

    private void HandleHostLog(HostLogItem log) => LogItems.Add(log);

    private void HandleDiagnostic(string message)
    {
        var item = new HostLogItem(DateTimeOffset.Now.ToString("HH:mm:ss"), "Diagnostic", "system", message, null);
        LogItems.Add(item);
        if (message.Contains("[CrashRisk]", StringComparison.OrdinalIgnoreCase))
        {
            Status = "Crash risk detected";
            CrashRiskMessage = message;
            HasCrashRisk = true;
        }
    }

    private void ValidateConfigPath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            ConfigValidationMessage = "Please select a batch config file.";
            Status = "Select a config file first.";
            return;
        }
        if (!File.Exists(value))
        {
            ConfigValidationMessage = "Config file does not exist.";
            Status = "Config file not found.";
            return;
        }
        ConfigValidationMessage = string.Empty;
    }

    private void RebuildQueueView()
    {
        var filtered = TaskQueueItems.Where(item =>
            (QueueStatusFilter == "All" || string.Equals(item.Status, QueueStatusFilter, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(QueueSearchText) ||
             item.FilePath.Contains(QueueSearchText, StringComparison.OrdinalIgnoreCase) ||
             item.HostVersion.Contains(QueueSearchText, StringComparison.OrdinalIgnoreCase)));

        VisibleQueueItems.Clear();
        foreach (var item in filtered)
            VisibleQueueItems.Add(item);
        OnPropertyChanged(nameof(HasVisibleQueueItems));
        OnPropertyChanged(nameof(IsQueueEmpty));
        UpdateSelectedQueueCount();
    }

    private void OnQueueCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (var item in e.NewItems.OfType<QueueTaskItem>())
                item.PropertyChanged += OnQueueItemPropertyChanged;
        if (e.OldItems != null)
            foreach (var item in e.OldItems.OfType<QueueTaskItem>())
                item.PropertyChanged -= OnQueueItemPropertyChanged;
        RebuildQueueView();
    }

    private void OnQueueItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(QueueTaskItem.IsSelected))
            UpdateSelectedQueueCount();
    }

    private void UpdateSelectedQueueCount()
    {
        SelectedQueueCount = TaskQueueItems.Count(item => item.IsSelected);
        _updatingSelectAllQueueItems = true;
        SelectAllQueueItems = VisibleQueueItems.Count > 0 && VisibleQueueItems.All(item => item.IsSelected);
        _updatingSelectAllQueueItems = false;
        RemoveSelectedCommand.NotifyCanExecuteChanged();
        AssignVersionCommand.NotifyCanExecuteChanged();
        ChangeViewCommand.NotifyCanExecuteChanged();
    }

    private void ReindexQueueItems()
    {
        for (var i = 0; i < TaskQueueItems.Count; i++)
            TaskQueueItems[i].Index = i + 1;
    }

    private void AppendUiDiagnostic(string message) => HandleDiagnostic(message);

    private void OnLogItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(HasLogItems));
        OnPropertyChanged(nameof(IsLogEmpty));
    }
}

