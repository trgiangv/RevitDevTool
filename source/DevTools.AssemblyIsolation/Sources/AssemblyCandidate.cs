using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace DevTools.AssemblyIsolation.Sources;

public sealed record AssemblyCandidate
{
    public AssemblyCandidate(string path, string sourceName, string allowedRoot)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("A candidate path is required.", nameof(path));
        if (string.IsNullOrWhiteSpace(sourceName))
            throw new ArgumentException("A source name is required.", nameof(sourceName));
        if (string.IsNullOrWhiteSpace(allowedRoot))
            throw new ArgumentException("An allowed root is required.", nameof(allowedRoot));

        Path = System.IO.Path.GetFullPath(path);
        SourceName = sourceName;
        AllowedRoot = System.IO.Path.GetFullPath(allowedRoot);

        if (!IsUnderAllowedRoot(Path, AllowedRoot))
            throw new ArgumentException("The candidate path must be contained by its allowed root.", nameof(path));
    }

    public string Path { get; }

    public string SourceName { get; }

    public string AllowedRoot { get; }

    public void Deconstruct(out string path, out string sourceName, out string allowedRoot)
    {
        path = Path;
        sourceName = SourceName;
        allowedRoot = AllowedRoot;
    }

    internal static AssemblyCandidate? TryCreate(string path, string sourceName, string allowedRoot)
    {
        try
        {
            return new AssemblyCandidate(path, sourceName, allowedRoot);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    internal static bool IsUnderAllowedRoot(string path, string allowedRoot)
    {
        var normalizedPath = System.IO.Path.GetFullPath(path);
        var normalizedRoot = System.IO.Path.GetFullPath(allowedRoot);
        var prefix = normalizedRoot.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? normalizedRoot
            : normalizedRoot + System.IO.Path.DirectorySeparatorChar;

        return normalizedPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsExistingPathUnderAllowedRoot(string path, string allowedRoot)
    {
        if (!File.Exists(path) || !Directory.Exists(allowedRoot))
            return false;

        return TryGetFinalPath(path, out var finalPath)
               && TryGetFinalPath(allowedRoot, out var finalRoot)
               && IsUnderCanonicalRoot(finalPath, finalRoot);
    }

    static bool IsUnderCanonicalRoot(string path, string root)
    {
        var normalizedRoot = root.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
        var prefix = normalizedRoot + System.IO.Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    static bool TryGetFinalPath(string path, out string finalPath)
    {
        using var handle = CreateFile(
            path,
            FileReadAttributes,
            FileShareRead | FileShareWrite | FileShareDelete,
            IntPtr.Zero,
            OpenExisting,
            FileFlagBackupSemantics,
            IntPtr.Zero);
        if (handle.IsInvalid)
        {
            finalPath = null!;
            return false;
        }

        var buffer = new StringBuilder(260);
        while (true)
        {
            var length = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Capacity, 0);
            if (length == 0)
            {
                finalPath = null!;
                return false;
            }

            if (length < buffer.Capacity)
            {
                finalPath = buffer.ToString().TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
                return true;
            }

            buffer = new StringBuilder(checked((int)length + 1));
        }
    }

    const uint FileReadAttributes = 0x80;
    const uint FileShareRead = 0x1;
    const uint FileShareWrite = 0x2;
    const uint FileShareDelete = 0x4;
    const uint OpenExisting = 3;
    const uint FileFlagBackupSemantics = 0x02000000;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern uint GetFinalPathNameByHandle(
        SafeFileHandle file,
        StringBuilder path,
        uint pathLength,
        uint flags);
}
