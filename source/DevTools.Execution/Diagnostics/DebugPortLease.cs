using System.Net;
using System.Net.Sockets;

namespace DevTools.Execution.Diagnostics;

/// <summary>
/// Exclusive IPv4 loopback bind plus a named mutex so another DevTools
/// process cannot steal the preferred debugger port while we hold it.
/// Dispose before <c>debugpy.listen</c> / <c>pydevd._enable_attach</c> so
/// those runtimes can bind the same number.
/// </summary>
public sealed class DebugPortLease : IDisposable
{
    public const string Host = "127.0.0.1";

    private static readonly IPAddress LoopbackV4 = IPAddress.Parse(Host);

    private readonly Socket _socket;
    private readonly Mutex? _mutex;
    private bool _disposed;

    private DebugPortLease(Socket socket, Mutex? mutex, int port, bool isPreferred)
    {
        _socket = socket;
        _mutex = mutex;
        Port = port;
        IsPreferred = isPreferred;
    }

    public int Port { get; }
    public bool IsPreferred { get; }

    public static DebugPortLease Acquire(int preferredPort)
    {
        if (TryBind(preferredPort, out var lease))
            return lease;

        if (preferredPort != 0 && TryBind(0, out lease))
            return lease;

        throw new InvalidOperationException("No 127.0.0.1 TCP port available for the debugger.");
    }

    private static bool TryBind(int port, out DebugPortLease lease)
    {
        lease = null!;
        Mutex? mutex = null;
        Socket? socket = null;
        try
        {
            if (port != 0 && !TryOwnMutex(port, out mutex))
                return false;

            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                ExclusiveAddressUse = true,
            };
            socket.Bind(new IPEndPoint(LoopbackV4, port));
            socket.Listen(1);
            var bound = ((IPEndPoint)socket.LocalEndPoint!).Port;
            lease = new DebugPortLease(socket, mutex, bound, isPreferred: port != 0 && bound == port);
            return true;
        }
        catch (AbandonedMutexException)
        {
            DisposeQuietly(socket);
            DisposeQuietly(mutex);
            return false;
        }
        catch (SocketException)
        {
            DisposeQuietly(socket);
            DisposeQuietly(mutex);
            return false;
        }
        catch
        {
            DisposeQuietly(socket);
            DisposeQuietly(mutex);
            throw;
        }
    }

    private static bool TryOwnMutex(int port, out Mutex? mutex)
    {
        mutex = null;
        var name = $@"Local\DevTools.DebugPort.{port}";
        var created = new Mutex(initiallyOwned: false, name);
        try
        {
            if (created.WaitOne(0))
            {
                mutex = created;
                return true;
            }

            created.Dispose();
            return false;
        }
        catch (AbandonedMutexException)
        {
            mutex = created;
            return true;
        }
        catch
        {
            created.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        DisposeQuietly(_socket);
        DisposeQuietly(_mutex);
    }

    private static void DisposeQuietly(IDisposable? resource)
    {
        try
        {
            resource?.Dispose();
        }
        catch
        {
            // Port hand-off; a dispose race must not fail listen.
        }
    }
}
