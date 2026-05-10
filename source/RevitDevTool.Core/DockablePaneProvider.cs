using System.Windows;
namespace RevitDevTool.Core;

/// <summary>
///     Provides access to create a new dockable pane to the Revit user interface.
/// </summary>
/// <remarks>
///     Two content modes:
///     <list type="bullet">
///         <item>
///             <b>Static:</b> In <see cref="SetupDockablePane" />, set <see cref="DockablePaneProviderData.FrameworkElement" />
///             and do not call <see cref="SetFrameworkElementFactory" />. <see cref="CreateFrameworkElement" /> is not used.
///         </item>
///         <item>
///             <b>Factory:</b> Call <see cref="SetFrameworkElementFactory" /> before <see cref="SetConfiguration" />.
///             In setup, only configure <see cref="DockablePaneProviderData.InitialState" />.
///             Revit requires <see cref="DockablePaneProviderData.FrameworkElement" /> to stay unset and
///             <see cref="DockablePaneProviderData.FrameworkElementCreator" /> to reference an
///             <see cref="IFrameworkElementCreator" /> — this type assigns itself after your handler runs.
///         </item>
///     </list>
/// </remarks>
[PublicAPI]
public class DockablePaneProvider : IDockablePaneProvider, IFrameworkElementCreator
{
#nullable disable // Nullable values controlled by Fluent API
    private UIControlledApplication _application;
    private DockablePaneId _id;
    private Action<DockablePaneProviderData> _setupHandler;
    private string _title;
    private Func<FrameworkElement> _elementFactory;
    private bool _registered;
#nullable restore

    private DockablePaneProvider()
    {
    }

    /// <summary>
    ///     Method called during initialization of the user interface to gather information about a dockable pane window.
    /// </summary>
    /// <param name="data">Container for information about the new dockable pane.</param>
    public void SetupDockablePane(DockablePaneProviderData data)
    {
        _setupHandler(data);
        if (_elementFactory is null) return;
        data.FrameworkElement = null;
        data.FrameworkElementCreator = this;
    }

    /// <summary>
    ///     Method called by Revit to create the FrameworkElement to be hosted in the dockable pane.
    /// </summary>
    public FrameworkElement CreateFrameworkElement()
    {
        return _elementFactory?.Invoke() ?? null!;
    }

    /// <summary>
    ///     Supplies the root element when using the framework element creator path (see class remarks).
    ///     Must be called before <see cref="SetConfiguration" />.
    /// </summary>
    public DockablePaneProvider SetFrameworkElementFactory(Func<FrameworkElement> factory)
    {
        ThrowIfRegistered();
        _elementFactory = factory ?? throw new ArgumentNullException(nameof(factory));
        return this;
    }

    /// <summary>
    ///     Sets the Id of the dockable pane.
    /// </summary>
    /// <param name="id">Unique identifier for the new pane.</param>
    public DockablePaneProvider SetId(Guid id)
    {
        _id = new DockablePaneId(id);
        return this;
    }

    /// <summary>
    ///     Sets the Id of the dockable pane.
    /// </summary>
    /// <param name="id">Unique identifier for the new pane.</param>
    public DockablePaneProvider SetId(DockablePaneId id)
    {
        _id = id;
        return this;
    }

    /// <summary>
    ///     Sets the title of the dockable pane.
    /// </summary>
    /// <param name="title">String to use for the pane caption.</param>
    public DockablePaneProvider SetTitle(string title)
    {
        _title = title;
        return this;
    }

    /// <summary>
    ///     Sets the configuration of the dockable pane and registers it with Revit.
    /// </summary>
    /// <param name="handler">
    ///     Configuration handler. Provides a container for information about the new dockable pane.
    ///     Set <see cref="DockablePaneProviderData.FrameworkElement" /> and
    ///     <see cref="DockablePaneProviderData.InitialState" /> for static content, or only
    ///     <see cref="DockablePaneProviderData.InitialState" /> when a framework element factory was configured.
    /// </param>
    public void SetConfiguration(Action<DockablePaneProviderData> handler)
    {
        ThrowIfRegistered();
        _setupHandler = handler ?? throw new ArgumentNullException(nameof(handler));
        _application.RegisterDockablePane(_id, _title, this);
        _registered = true;
    }

    private void ThrowIfRegistered()
    {
        if (_registered)
            throw new InvalidOperationException("Dockable pane configuration is already registered with Revit.");
    }

    /// <summary>
    ///     Creates a new <see cref="DockablePaneProvider" /> instance.
    /// </summary>
    /// <param name="application">The UIControlledApplication.</param>
    public static DockablePaneProvider Register(UIControlledApplication application)
    {
        return new DockablePaneProvider
        {
            _application = application
        };
    }

    /// <summary>
    ///     Creates a new <see cref="DockablePaneProvider" /> instance pre-configured with id and title.
    /// </summary>
    /// <param name="application">The UIControlledApplication.</param>
    /// <param name="id">Unique identifier for the new pane.</param>
    /// <param name="title">String to use for the pane caption.</param>
    public static DockablePaneProvider Register(UIControlledApplication application, Guid id, string title)
    {
        return Register(application).SetId(id).SetTitle(title);
    }

    /// <summary>
    ///     Creates a new <see cref="DockablePaneProvider" /> instance pre-configured with id and title.
    /// </summary>
    /// <param name="application">The UIControlledApplication.</param>
    /// <param name="id">Unique identifier for the new pane.</param>
    /// <param name="title">String to use for the pane caption.</param>
    public static DockablePaneProvider Register(UIControlledApplication application, DockablePaneId id, string title)
    {
        return Register(application).SetId(id).SetTitle(title);
    }
}