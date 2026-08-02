using System.Reflection;
using System.Runtime.InteropServices;
using Autodesk.Revit.UI.Events;

namespace RevitDevTool.Core;

/// <summary>
///     Provides static access to the current Revit session objects and API-mode detection.
/// </summary>
/// <remarks>
///     Properties like <see cref="UiApplication"/> and <see cref="ActiveDocument"/> are
///     available for the lifetime of the Revit session without requiring an explicit reference
///     to be passed around.
/// </remarks>
[PublicAPI]
public static class RevitContext
{
    private static readonly RibbonItemEventArgs RibbonItemEventArgs = new();
    private static UIControlledApplication? _uiControlledApplication;
    private static readonly Func<bool> GetIsInApiContext;
    private static readonly IntPtr IncrementConstructorPointer;
    private static readonly IntPtr IncrementDestructorPointer;
    
    [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
    private delegate IntPtr IncrementCtor(IntPtr self);

    [UnmanagedFunctionPointer(CallingConvention.ThisCall)]
    private delegate void IncrementDtor(IntPtr self);

    /// <summary>
    ///  Internal API via reflection inspired by RevitToolkit
    ///  https://github.com/Nice3point/RevitToolkit
    /// </summary>
    /// <exception cref="NotSupportedException"></exception>
    static RevitContext()
    {
        var assemblies = FindAssemblies("APIUIAPI", "RevitAPIUI");

        var apiMethods = assemblies[0].ManifestModule.GetMethods(BindingFlags.NonPublic | BindingFlags.Static);
        var singletonFactory = apiMethods.FirstOrDefault(m => m.Name == "APICallDepthManager.singletonfactory")
                               ?? throw new NotSupportedException("Cannot resolve APICallDepthManager.singletonfactory");
        var isInApiMode = apiMethods.FirstOrDefault(m => m.Name == "APICallDepthManager.isRevitInAPIMode")
                          ?? throw new NotSupportedException("Cannot resolve APICallDepthManager.isRevitInAPIMode");

        var uiAssemblyMethods =  assemblies[1].ManifestModule.GetMethods(BindingFlags.NonPublic | BindingFlags.Static);
        var incrementConstructor = uiAssemblyMethods.FirstOrDefault(method => method.Name == "IncrementAPICallDepth.{ctor}") 
                                   ?? throw new NotSupportedException("Cannot resolve IncrementAPICallDepth constructor");
        var incrementDestructor = uiAssemblyMethods.FirstOrDefault(method => method.Name == "IncrementAPICallDepth.{dtor}")
                                   ?? throw new NotSupportedException("Cannot resolve IncrementAPICallDepth destructor");
        
        IncrementConstructorPointer = incrementConstructor.MethodHandle.GetFunctionPointer();
        IncrementDestructorPointer = incrementDestructor.MethodHandle.GetFunctionPointer();
        GetIsInApiContext = () =>
        {
            var manager = singletonFactory.Invoke(null, null);
            return (bool)isInApiMode.Invoke(null, [manager])!;
        };
    }

    /// <summary>
    ///     Active <see cref="Autodesk.Revit.UI.UIApplication"/> for the current Revit session.
    /// </summary>
    public static UIApplication UiApplication => RibbonItemEventArgs.Application;

    /// <summary>
    ///     <see cref="Autodesk.Revit.UI.UIControlledApplication"/> instance for the current session.
    ///     Created lazily via internal constructor reflection.
    /// </summary>
    public static UIControlledApplication UiControlledApplication => _uiControlledApplication ??= CreateUiControlledApplication();

    /// <summary>
    ///     Database-level <see cref="Autodesk.Revit.ApplicationServices.Application"/> for the current session.
    /// </summary>
    public static Autodesk.Revit.ApplicationServices.Application Application => UiApplication.Application;

    /// <summary>
    ///     Currently active project at the UI level, or <see langword="null"/> if none.
    /// </summary>
    public static UIDocument? ActiveUiDocument => UiApplication.ActiveUIDocument;

    /// <summary>
    ///     Currently active <see cref="Autodesk.Revit.DB.Document"/>, or <see langword="null"/> if none.
    /// </summary>
    public static Document? ActiveDocument => UiApplication.ActiveUIDocument?.Document;

    /// <summary>
    ///     Active <see cref="Autodesk.Revit.DB.View"/> of the active document, or <see langword="null"/>.
    /// </summary>
    /// <exception cref="System.InvalidOperationException">
    ///     Thrown when setting the property while no document is open, a transaction is active,
    ///     the document is read-only, or during restricted events.
    /// </exception>
    public static View? ActiveView
    {
        get => UiApplication.ActiveUIDocument?.ActiveView;
        set
        {
            if (UiApplication.ActiveUIDocument is null) throw new InvalidOperationException("There are no active documents in the current Autodesk Revit session");
            UiApplication.ActiveUIDocument.ActiveView = value;
        }
    }

    /// <summary>
    ///     Active graphical <see cref="Autodesk.Revit.DB.View"/> of the active document, or <see langword="null"/>.
    /// </summary>
    public static View? ActiveGraphicalView => UiApplication.ActiveUIDocument?.ActiveGraphicalView;

    /// <summary>
    ///     Returns <see langword="true"/> when the current thread is inside a valid Revit API context.
    /// </summary>
    /// <remarks>
    ///     Implemented via reflection on Revit's internal <c>APICallDepthManager</c> for a fast,
    ///     side-effect-free check (~1-5 us).
    /// </remarks>
    public static bool IsRevitInApiMode => GetIsInApiContext();

    private static UIControlledApplication CreateUiControlledApplication()
    {
        return (UIControlledApplication)Activator.CreateInstance(
            typeof(UIControlledApplication),
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            [UiApplication],
            null)!;
    }
    
    /// <summary>
    ///     Finds assemblies loaded in the current application domain that match the specified names.
    /// </summary>
    private static Assembly[] FindAssemblies(params string[] names)
    {
        var remaining = new HashSet<string>(names);
        var result = new Dictionary<string, Assembly>(names.Length);

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var name = assembly.GetName().Name;
            if (name is null || !remaining.Remove(name)) continue;
            result[name] = assembly;
            if (remaining.Count == 0) break;
        }

        if (remaining.Count <= 0) 
            return names.Select(name => result[name]).ToArray();
        var missing = string.Join(", ", remaining);
        throw new NotSupportedException($"Cannot find assemblies: {missing}");
    }
    
    internal static IDisposable BeginApiContextScope()
    {
        return new ApiContextScope(IncrementConstructorPointer, IncrementDestructorPointer);
    }
    
    private sealed class ApiContextScope : IDisposable
    {
        private readonly IntPtr _memory;
        private readonly IntPtr _deconstructorPointer;
        private int _disposed;

        internal ApiContextScope(IntPtr constructorPointer, IntPtr deconstructorPointer)
        {
            _deconstructorPointer = deconstructorPointer;
            _memory = Marshal.AllocHGlobal(8);
            Marshal.WriteInt64(_memory, 0);

            var constructorDelegate = Marshal.GetDelegateForFunctionPointer<IncrementCtor>(constructorPointer);
            constructorDelegate(_memory);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

            var deconstructorDelegate = Marshal.GetDelegateForFunctionPointer<IncrementDtor>(_deconstructorPointer);
            deconstructorDelegate(_memory);

            Marshal.FreeHGlobal(_memory);
        }
    }
}
