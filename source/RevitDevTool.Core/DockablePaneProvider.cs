namespace RevitDevTool.Core;

/// <summary>
///     Provides access to create a new dockable pane to the Revit user interface.
/// </summary>
[PublicAPI]
public class DockablePaneProvider : IDockablePaneProvider
{
#nullable disable //Nullable values controlled by Fluent API
    private UIControlledApplication _application;
    private DockablePaneId _id;
    private Action<DockablePaneProviderData> _setupHandler;
    private string _title;
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
    ///     Implementers should set the FrameworkElement and InitialState properties.
    /// </param>
    public void SetConfiguration(Action<DockablePaneProviderData> handler)
    {
        _setupHandler = handler;
        _application.RegisterDockablePane(_id, _title, this);
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