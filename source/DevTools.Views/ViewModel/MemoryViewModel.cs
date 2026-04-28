using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Threading;
using Brush = System.Windows.Media.Brush;
using Color = System.Windows.Media.Color;

namespace DevTools.Views.ViewModel;

public partial class MemoryViewModel : ObservableObject
{
    private enum MemoryJudgement
    {
        Good = 0,
        Warning = 1,
        Critical = 2
    }

    private static readonly Brush GoodBrush = CreateBrush(34, 197, 94);
    private static readonly Brush WarningBrush = CreateBrush(234, 179, 8);
    private static readonly Brush CriticalBrush = CreateBrush(239, 68, 68);
    private static readonly Brush NeutralBrush = CreateBrush(148, 163, 184);

    private readonly DispatcherTimer _refreshTimer;
    private OperationSession? _activeSession;

    [ObservableProperty] private double _workingSetMb;
    [ObservableProperty] private double _privateMb;
    [ObservableProperty] private double _managedMb;
    [ObservableProperty] private int _handleCount;
    [ObservableProperty] private int _threadCount;
    [ObservableProperty] private string _lastOperationName = "N/A";
    [ObservableProperty] private string _lastOperationProvider = "-";
    [ObservableProperty] private bool _lastOperationSuccess;
    [ObservableProperty] private long _lastOperationDurationMs;
    [ObservableProperty] private double _lastDeltaPrivateMb;
    [ObservableProperty] private double _lastDeltaManagedMb;
    [ObservableProperty] private int _lastDeltaHandles;
    [ObservableProperty] private double _lastPeakPrivateDeltaMb;
    [ObservableProperty] private string _lastOutcomeText = "N/A";
    [ObservableProperty] private Brush _lastOutcomeBrush = NeutralBrush;
    [ObservableProperty] private Brush _lastDeltaPrivateBrush = NeutralBrush;
    [ObservableProperty] private Brush _lastDeltaManagedBrush = NeutralBrush;
    [ObservableProperty] private Brush _lastDeltaHandlesBrush = NeutralBrush;

    public MemoryViewModel()
    {
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _refreshTimer.Tick += (_, _) => RefreshMemoryUsage();
    }

    public void Start()
    {
        if (_refreshTimer.IsEnabled) return;
        RefreshMemoryUsage();
        _refreshTimer.Start();
    }

    public void Stop() => _refreshTimer.Stop();

    private void RefreshMemoryUsage()
    {
        var snapshot = CaptureSnapshot();
        ApplyRealtimeSnapshot(snapshot);
        UpdateActiveSessionPeak(snapshot);
    }

    public OperationScope BeginOperation(string provider, string target)
    {
        var normalizedProvider = string.IsNullOrWhiteSpace(provider) ? "-" : provider;
        var normalizedTarget = string.IsNullOrWhiteSpace(target) ? "-" : target;
        var before = CaptureSnapshot();
        _activeSession = new OperationSession(normalizedProvider, normalizedTarget, before);
        return new OperationScope(this);
    }

    private void CompleteOperation(bool success, long durationMs)
    {
        if (_activeSession == null) return;

        var after = CaptureSnapshot();
        var peakPrivateMb = Math.Max(_activeSession.Value.Peak.PrivateMb, after.PrivateMb);

        LastOperationName = _activeSession.Value.Target;
        LastOperationProvider = _activeSession.Value.Provider;
        LastOperationSuccess = success;
        LastOperationDurationMs = durationMs;
        LastDeltaPrivateMb = after.PrivateMb - _activeSession.Value.Before.PrivateMb;
        LastPeakPrivateDeltaMb = peakPrivateMb - _activeSession.Value.Before.PrivateMb;
        LastDeltaManagedMb = after.ManagedMb - _activeSession.Value.Before.ManagedMb;
        LastDeltaHandles = after.HandleCount - _activeSession.Value.Before.HandleCount;
        LastOutcomeText = success ? "Success" : "Failed";
        LastOutcomeBrush = success ? GoodBrush : CriticalBrush;

        LastDeltaPrivateBrush = ToBrush(EvaluateDeltaMb(LastDeltaPrivateMb, warning: 30, critical: 80));
        LastDeltaManagedBrush = ToBrush(EvaluateDeltaMb(LastDeltaManagedMb, warning: 20, critical: 50));
        LastDeltaHandlesBrush = ToBrush(EvaluateDeltaInt(LastDeltaHandles, warning: 20, critical: 60));

        _activeSession = null;
        ApplyRealtimeSnapshot(after);
    }

    private static Brush ToBrush(MemoryJudgement judgement) => judgement switch
    {
        MemoryJudgement.Critical => CriticalBrush,
        MemoryJudgement.Warning => WarningBrush,
        _ => GoodBrush
    };

    private static MemoryJudgement EvaluateDeltaMb(double delta, double warning, double critical) =>
        delta >= critical ? MemoryJudgement.Critical : delta >= warning ? MemoryJudgement.Warning : MemoryJudgement.Good;

    private static MemoryJudgement EvaluateDeltaInt(int delta, int warning, int critical) =>
        delta >= critical ? MemoryJudgement.Critical : delta >= warning ? MemoryJudgement.Warning : MemoryJudgement.Good;

    private static SolidColorBrush CreateBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private static MemorySnapshot CaptureSnapshot()
    {
        using var process = Process.GetCurrentProcess();
        return new MemorySnapshot(
            process.WorkingSet64 / 1024d / 1024d,
            process.PrivateMemorySize64 / 1024d / 1024d,
            GC.GetTotalMemory(false) / 1024d / 1024d,
            process.HandleCount,
            process.Threads.Count);
    }

    private void ApplyRealtimeSnapshot(MemorySnapshot snapshot)
    {
        WorkingSetMb = snapshot.RamMb;
        PrivateMb = snapshot.PrivateMb;
        ManagedMb = snapshot.ManagedMb;
        HandleCount = snapshot.HandleCount;
        ThreadCount = snapshot.ThreadCount;
    }

    private void UpdateActiveSessionPeak(MemorySnapshot snapshot)
    {
        if (_activeSession == null) return;
        if (snapshot.PrivateMb > _activeSession.Value.Peak.PrivateMb)
            _activeSession = _activeSession.Value with { Peak = snapshot };
    }

    private readonly record struct MemorySnapshot(double RamMb, double PrivateMb, double ManagedMb, int HandleCount, int ThreadCount);

    private readonly record struct OperationSession(string Provider, string Target, MemorySnapshot Before, MemorySnapshot Peak)
    {
        public OperationSession(string provider, string target, MemorySnapshot before)
            : this(provider, target, before, before) { }
    }

    public sealed class OperationScope(MemoryViewModel owner) : IDisposable
    {
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private MemoryViewModel? _owner = owner;
        private bool _completed;

        public void Complete(bool success)
        {
            if (_completed || _owner == null) return;
            _completed = true;
            _owner.CompleteOperation(success, _stopwatch.ElapsedMilliseconds);
            _owner = null;
        }

        public void Dispose() => Complete(success: false);
    }
}
