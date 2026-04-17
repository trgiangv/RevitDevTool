using System.Reflection;
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

    /// <summary>
    ///  Internal API via reflection inspired by RevitToolkit
    ///  https://github.com/Nice3point/RevitToolkit
    /// </summary>
    /// <exception cref="NotSupportedException"></exception>
    static RevitContext()
    {
        var assembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "APIUIAPI")
            ?? throw new NotSupportedException("Cannot find APIUIAPI assembly");

        var methods = assembly.ManifestModule.GetMethods(BindingFlags.NonPublic | BindingFlags.Static);
        var singletonFactory = methods.FirstOrDefault(m => m.Name == "APICallDepthManager.singletonfactory")
                               ?? throw new NotSupportedException("Cannot resolve APICallDepthManager.singletonfactory");
        var isInApiMode = methods.FirstOrDefault(m => m.Name == "APICallDepthManager.isRevitInAPIMode")
                          ?? throw new NotSupportedException("Cannot resolve APICallDepthManager.isRevitInAPIMode");

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

    /// <inheritdoc cref="ActiveDocument"/>
    [Obsolete("Use RevitContext.ActiveDocument instead.")]
    public static Document? Document => ActiveDocument;

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
}
