using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
namespace RevitDevTool.Core.DockableLoader;

/// <summary>
///     Hosts WPF content from a satellite DLL using <see cref="PaneLoadContext" /> so pane reload stays isolated
///     from command-runner loading and <see cref="Reload" /> can pick up a rebuilt assembly without restarting Revit
///     (.NET Core / modern TFMs only).
/// </summary>
[PublicAPI]
public sealed class CollectibleAssemblyPane : ContentControl, IDisposable
{
    /// <summary>
    ///     Optional reload hook for satellite UI loaded in a collectible ALC (cannot reference host assemblies directly).
    ///     Host sets this before <see cref="Load" />; satellite invokes via reflection on this type (main assembly name).
    /// </summary>
    public static Action? PaneReload { get; set; }

    private void ApplyHostDefaults()
    {
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        VerticalContentAlignment = VerticalAlignment.Stretch;
        Focusable = true;
    }

#if NET
    private PaneLoadContext? _alc;

    /// <summary>
    ///     Loads <paramref name="fullTypeName" /> from <paramref name="assemblyPath" /> into a new collectible context.
    /// </summary>
    public static CollectibleAssemblyPane Load(string assemblyPath, string fullTypeName)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath))
            throw new ArgumentException("Assembly path is required.", nameof(assemblyPath));
        if (string.IsNullOrWhiteSpace(fullTypeName))
            throw new ArgumentException("Full type name is required.", nameof(fullTypeName));

        var host = new CollectibleAssemblyPane();
        host.ApplyHostDefaults();
        host.ReloadCore(assemblyPath, fullTypeName);
        return host;
    }

    /// <inheritdoc cref="Load" />
    public void Reload(string assemblyPath, string fullTypeName)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath))
            throw new ArgumentException("Assembly path is required.", nameof(assemblyPath));
        if (string.IsNullOrWhiteSpace(fullTypeName))
            throw new ArgumentException("Full type name is required.", nameof(fullTypeName));

        ReloadCore(assemblyPath, fullTypeName);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private void ReloadCore(string assemblyPath, string fullTypeName)
    {
        ClearLoaded();

        var alc = new PaneLoadContext(assemblyPath);
        try
        {
            var element = LoadElementInContext(alc, assemblyPath, fullTypeName);
            Content = element;
            _alc = alc;
            alc = null;
        }
        finally
        {
            alc?.Unload();
        }
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static FrameworkElement LoadElementInContext(PaneLoadContext alc, string assemblyPath, string fullTypeName)
    {
        using var stream = new FileStream(assemblyPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        var pdbPath = Path.ChangeExtension(assemblyPath, ".pdb");
        Assembly assembly;
        if (File.Exists(pdbPath))
        {
            using var symbolStream = new FileStream(pdbPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            assembly = alc.LoadFromStream(stream, symbolStream);
        }
        else
        {
            assembly = alc.LoadFromStream(stream);
        }

        var instance = assembly.CreateInstance(fullTypeName);
        return instance switch
        {
            null => throw new InvalidOperationException($"Could not create instance of '{fullTypeName}'."),
            FrameworkElement fe => fe,
            _ => throw new InvalidOperationException($"Type '{fullTypeName}' must derive from FrameworkElement.")
        };
    }

    private void ClearLoaded()
    {
        Content = null;
        if (_alc is null)
            return;

        try
        {
            _alc.Unload();
        }
        catch (Exception ex)
        {
            Trace.TraceWarning($"CollectibleAssemblyPane: ALC unload failed: {ex.Message}");
        }

        _alc = null;
        RevitContext.Application.PurgeReleasedAPIObjects();
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        ClearLoaded();
    }
#else
    /// <summary>
    ///     Loads <paramref name="fullTypeName" /> from <paramref name="assemblyPath" /> (no collectible reload on .NET Framework).
    /// </summary>
    public static CollectibleAssemblyPane Load(string assemblyPath, string fullTypeName)
    {
        if (string.IsNullOrWhiteSpace(assemblyPath))
            throw new ArgumentException("Assembly path is required.", nameof(assemblyPath));
        if (string.IsNullOrWhiteSpace(fullTypeName))
            throw new ArgumentException("Full type name is required.", nameof(fullTypeName));

        var host = new CollectibleAssemblyPane();
        host.ApplyHostDefaults();
        var bytes = File.ReadAllBytes(assemblyPath);
        var assembly = Assembly.Load(bytes);
        var instance = assembly.CreateInstance(fullTypeName);
        host.Content = instance switch
        {
            null => throw new InvalidOperationException($"Could not create instance of '{fullTypeName}'."),
            FrameworkElement fe => fe,
            _ => throw new InvalidOperationException($"Type '{fullTypeName}' must derive from FrameworkElement.")
        };
        return host;
    }

    /// <summary>
    ///     Not supported on .NET Framework — rebuilds require restarting Revit for satellite DLLs.
    /// </summary>
    public void Reload(string assemblyPath, string fullTypeName)
    {
        Trace.TraceWarning(
            "CollectibleAssemblyPane.Reload is not supported on .NET Framework; restart Revit to load a rebuilt satellite DLL.");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Content = null;
        GC.SuppressFinalize(this);
    }
#endif
}
