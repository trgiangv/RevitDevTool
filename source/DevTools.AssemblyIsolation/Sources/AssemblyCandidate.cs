using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace DevTools.AssemblyIsolation.Sources;

public sealed record AssemblyCandidate
{
    const string Extension = ".dll";

    public AssemblyCandidate(string path, string root)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("A candidate path is required.", nameof(path));
        if (string.IsNullOrWhiteSpace(root))
            throw new ArgumentException("A root is required.", nameof(root));

        Path = System.IO.Path.GetFullPath(path);
        Root = System.IO.Path.GetFullPath(root);

        if (!IsUnderRoot(Path, Root))
            throw new ArgumentException("The candidate path must be contained by its root.", nameof(path));
    }

    public string Path { get; }

    public string Root { get; }

    public void Deconstruct(out string path, out string root)
    {
        path = Path;
        root = Root;
    }

    internal static AssemblyCandidate? TryCreate(string path, string root)
    {
        try
        {
            return new AssemblyCandidate(path, root);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    internal static string Combine(string directory, string simpleName) =>
        System.IO.Path.Combine(directory, simpleName + Extension);

    internal static string WithExtension(string fileName) =>
        fileName.EndsWith(Extension, StringComparison.OrdinalIgnoreCase)
            ? fileName
            : fileName + Extension;

    internal static string SearchPattern => "*" + Extension;

    internal static IEnumerable<string> LookupKeys(string pathOrName)
    {
        var fileName = System.IO.Path.GetFileName(pathOrName);
        if (string.IsNullOrWhiteSpace(fileName))
            yield break;

        yield return fileName;

        if (!fileName.EndsWith(Extension, StringComparison.OrdinalIgnoreCase))
            yield return fileName + Extension;

        var withoutExtension = System.IO.Path.GetFileNameWithoutExtension(fileName);
        if (!string.IsNullOrWhiteSpace(withoutExtension))
            yield return withoutExtension;
    }

    internal static bool IsUnderRoot(string path, string root)
    {
        var normalizedPath = System.IO.Path.GetFullPath(path);
        var normalizedRoot = System.IO.Path.GetFullPath(root);
        var prefix = normalizedRoot.EndsWith(System.IO.Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
            ? normalizedRoot
            : normalizedRoot + System.IO.Path.DirectorySeparatorChar;

        return normalizedPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsExistingPathUnderRoot(string path, string root)
    {
        if (!File.Exists(path) || !Directory.Exists(root))
            return false;

        return TryGetFinalPath(path, out var finalPath)
               && TryGetFinalPath(root, out var finalRoot)
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
