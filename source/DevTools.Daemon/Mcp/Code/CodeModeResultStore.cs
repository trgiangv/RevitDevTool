using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace DevTools.Daemon.Mcp.Code;

/// <summary>
/// Flat temp files for a <c>code_mode</c> return that is too long to send.
/// One save is <c>%TEMP%\RevitDevTool-{pid}-{id}.txt</c>.
/// </summary>
public sealed class CodeModeResultStore(string directory, int pid, long maxFileBytes, long maxProcessBytes)
{
    public const int ViewChars = 40_000;
    public const long MaxFileBytes = 64L * 1024 * 1024;
    public const long MaxProcessBytes = 256L * 1024 * 1024;

    public static CodeModeResultStore Shared { get; } = new(Path.GetTempPath(), Environment.ProcessId);

    private readonly Lock _gate = new();
    private long _ownedBytes;

    public CodeModeResultStore(string directory, int pid)
        : this(directory, pid, MaxFileBytes, MaxProcessBytes)
    {
    }

    public string Save(string text)
    {
        lock (_gate)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            if (_ownedBytes >= maxProcessBytes || _ownedBytes + bytes.Length > maxProcessBytes)
                throw new InvalidOperationException("Saved result exceeds 256 MB.");
            if (bytes.Length > maxFileBytes)
                throw new InvalidOperationException("Saved result exceeds 64 MB.");

            var path = Path.Combine(directory, FileName(pid, NewId()));
            Directory.CreateDirectory(directory);
            try
            {
                using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                stream.Write(bytes);
                _ownedBytes += bytes.Length;
                return path;
            }
            catch
            {
                if (File.Exists(path))
                    File.Delete(path);
                throw;
            }
        }
    }

    private static string NewId() => Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();

    private static string FileName(int pid, string id) => $"RevitDevTool-{pid}-{id}.txt";
}
