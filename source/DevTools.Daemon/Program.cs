using DevTools.Daemon.Control;
using DevTools.Update.Autodesk;
using DevTools.Update.Engine;
using DevTools.Update.Installations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Velopack;
using Velopack.Locators;

namespace DevTools.Daemon;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains(AppConstants.StdioArg, StringComparer.OrdinalIgnoreCase))
            return RunStdioAsync(args).GetAwaiter().GetResult();

        VelopackApp.Build()
            .SetArgs(args)
            .SetAutoApplyOnStartup(false)
            .OnAfterInstallFastCallback(_ => StartupRegistration.Enable())
            .OnBeforeUninstallFastCallback(_ =>
            {
                StartupRegistration.Disable();
                UpdateEngine.RemoveBundle(new AutodeskBundle(AutodeskBundle.DefaultRoot));
            })
            .Run();

        if (VelopackLocator.IsCurrentSet && VelopackLocator.Current.CurrentlyInstalledVersion is not null)
            LegacyInnoInstall.Remove(new AutodeskBundle(AutodeskBundle.DefaultRoot));

        if (args.Contains(AppConstants.SelectArg, StringComparer.OrdinalIgnoreCase))
            return RunSelectAsync(args).GetAwaiter().GetResult();

        var app = new App();
        app.InitializeComponent();
        return app.Run();
    }

    private static async Task<int> RunStdioAsync(string[] args)
    {
        try
        {
            using var host = AppHostBuilder.CreateStdioHost(args);
            await host.RunAsync().ConfigureAwait(false);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    /// <summary>
    ///     <c>--select "Revit 2024" --select "AutoCAD 2026" [--source &lt;RevitDevTool.bundle.zip&gt;] [--notify]</c>.
    ///     Installs payloads without UI (0047). A value is the <c>Description</c> of a <c>PackageContents.xml</c> component.
    /// </summary>
    private static async Task<int> RunSelectAsync(string[] args)
    {
        var components = PackageContents.TemplateComponents().ToDictionary(component => component.Description, StringComparer.OrdinalIgnoreCase);
        var names = Values(args, AppConstants.SelectArg).ToArray();
        var unknown = names.Where(name => !components.ContainsKey(name)).ToArray();
        if (names.Length == 0 || unknown.Length > 0)
        {
            Console.Error.WriteLine($"Unknown or missing --select value: {string.Join(", ", unknown)}. Known values: {string.Join(", ", components.Keys)}.");
            return 2;
        }

        using var host = AppHostBuilder.CreateUpdateHost();
        var engine = host.Services.GetRequiredService<UpdateEngine>();
        var policy = engine.Policy;
        policy.Mode = args.Contains(AppConstants.NotifyArg, StringComparer.OrdinalIgnoreCase) ? UpdateMode.Notify : UpdateMode.Auto;
        var selection = names.Select(name => components[name].Target).ToArray();
        var result = await engine.ApplySelectionAsync(selection, policy, Values(args, AppConstants.SourceArg).FirstOrDefault(), CancellationToken.None).ConfigureAwait(false);

        Console.WriteLine($"RevitDevTool {result.Version}: installed [{string.Join(", ", result.Applied)}], pending [{string.Join(", ", result.Pending)}], unavailable [{string.Join(", ", result.Unavailable)}].");
        StartupRegistration.Enable();
        return result.Unavailable.Count == 0 ? 0 : 3;
    }

    private static IEnumerable<string> Values(string[] args, string name) =>
        args.Select((arg, index) => (arg, index))
            .Where(item => string.Equals(item.arg, name, StringComparison.OrdinalIgnoreCase) && item.index + 1 < args.Length)
            .Select(item => args[item.index + 1]);
}
