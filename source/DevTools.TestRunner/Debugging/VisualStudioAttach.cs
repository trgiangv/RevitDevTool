using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using EnvDTE;

namespace DevTools.TestRunner.Debugging;

/// <summary>
/// Attaches Visual Studio to the CAD host via EnvDTE <c>LocalProcesses</c>.
/// Visual Studio owns the session after attach (Stop Debugging Detaches the guest).
/// </summary>
public sealed class VisualStudioAttach : IDebuggerAttach
{
    private const int RpcServerCallRetryLater = unchecked((int)0x8001010A);
    private const int RpcCallRejected = unchecked((int)0x80010001);

    public static VisualStudioAttach Instance { get; } = new();

    public bool TryAttach(AttachTarget target, TextWriter warnings)
    {
        using var sta = new StaWorker();
        return sta.Invoke(() => AttachOnSta(target, warnings), warnings);
    }

    private static bool AttachOnSta(AttachTarget target, TextWriter warnings)
    {
        var dte = GetActiveDte(warnings);
        if (dte is null)
        {
            warnings.WriteLine(
                "Visual Studio debugger was not found; host tests will run without an attached debugger.");
            return false;
        }

        var debugger = dte.Debugger;
        var host = FindHost(debugger, target.HostProcessId, warnings);
        if (host is null)
        {
            warnings.WriteLine(
                $"Visual Studio does not list host process {target.HostProcessId}; skipping debugger attach.");
            return false;
        }

        if (!IsBeingDebugged(debugger, target.HostProcessId))
        {
            RetryBusy(() =>
            {
                host.Attach();
                return true;
            }, warnings, "Attach()");
        }

        System.Threading.Thread.Sleep(1_000);
        return true;
    }

    private static Process? FindHost(Debugger debugger, int hostProcessId, TextWriter warnings) =>
        RetryBusy(
            () => debugger.LocalProcesses.OfType<Process>()
                .FirstOrDefault(candidate => candidate.ProcessID == hostProcessId),
            warnings,
            "LocalProcesses");

    private static bool IsBeingDebugged(Debugger debugger, int processId) =>
        debugger.DebuggedProcesses.OfType<Process>().Any(candidate => candidate.ProcessID == processId);

    private static T RetryBusy<T>(Func<T> action, TextWriter warnings, string what)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return action();
            }
            catch (COMException ex) when (IsBusy(ex) && attempt < 20)
            {
                warnings.WriteLine($"{what} busy 0x{ex.HResult:X8} retry {attempt}");
                System.Threading.Thread.Sleep(150);
            }
        }
    }

    private static bool IsBusy(COMException ex) =>
        ex.HResult is RpcServerCallRetryLater or RpcCallRejected;

    private static DTE? GetActiveDte(TextWriter warnings)
    {
        for (var version = 23; version >= 9; version--)
        {
            var progId = $"VisualStudio.DTE.{version}.0";
            try
            {
                if (MarshalUtils.GetActiveObject(progId) is DTE dte)
                    return dte;
            }
            catch (COMException)
            {
                // ProgId not registered for this VS version.
            }
            catch (Exception ex)
            {
                warnings.WriteLine($"GetActiveObject({progId}) {ex.GetType().Name}: {ex.Message}");
            }
        }

        return null;
    }

    private sealed class StaWorker : IDisposable
    {
        private readonly BlockingCollection<Action> _work = new();
        private readonly System.Threading.Thread _thread;

        public StaWorker()
        {
            _thread = new System.Threading.Thread(() =>
            {
                OleMessageFilter.Register();
                try
                {
                    foreach (var action in _work.GetConsumingEnumerable())
                        action();
                }
                finally
                {
                    OleMessageFilter.Unregister();
                }
            })
            {
                IsBackground = true,
                Name = "VS-attach"
            };
            _thread.SetApartmentState(ApartmentState.STA);
            _thread.Start();
        }

        public T Invoke<T>(Func<T> action, TextWriter warnings)
        {
            var finished = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            _work.Add(() =>
            {
                try
                {
                    finished.TrySetResult(action());
                }
                catch (Exception ex)
                {
                    finished.TrySetException(ex);
                }
            });

            if (!finished.Task.Wait(TimeSpan.FromSeconds(30)))
            {
                warnings.WriteLine("STA attach thread did not finish within 30s");
                return default!;
            }

            if (!finished.Task.IsCompletedSuccessfully)
            {
                var error = finished.Task.Exception?.GetBaseException();
                if (error is not null)
                    warnings.WriteLine($"{error.GetType().Name}: {error.Message}");
                return default!;
            }

            return finished.Task.Result;
        }

        public void Dispose()
        {
            _work.CompleteAdding();
            _thread.Join(TimeSpan.FromSeconds(5));
            _work.Dispose();
        }
    }
}
