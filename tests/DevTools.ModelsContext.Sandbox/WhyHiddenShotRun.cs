using DevTools.ModelsContext.Documents;
using DevTools.ModelsContext.Panel;
using DevTools.ModelsContext.Scope;
using DevTools.ModelsContext.Tests.Shared;
using DevTools.ModelsContext.Visibility;
using DevTools.Presentation.Views.ModelsContext.Panel;
using DevTools.UI.Theme;
using System.IO;
using System.Windows;

namespace DevTools.ModelsContext.Sandbox;

internal static class WhyHiddenShotRun
{
    public static async Task<int> CaptureAsync(SandboxWindow window, Application app, string directory)
    {
        ModelContextViewModel? panel = null;
        try
        {
            Directory.CreateDirectory(directory);
            var data = FakeModelGenerator.Generate(new FakeModelOptions { CurrentElements = 20_000, LinkElements = 5_000 });
            var host = new FakeHost(new FakeModel(data));
            panel = new ModelContextViewModel(host.Services(new SystemClock(), new DispatcherScheduler(window.Dispatcher),
                ConsoleLog.Current));
            panel.ContextBar.Scope = ScopeKind.WholeModel;
            window.Host.Content = new ModelContextView { DataContext = panel };
            await panel.InitializeAsync(CancellationToken.None);
            await Settle(1500);

            var ducts = panel.Explorer.Rows.First(row => row.Document == FakeModelGenerator.Current && row.Text == "Ducts");
            await panel.Explorer.ToggleEyeCommand.ExecuteAsync(ducts);
            var elements = panel.Explorer.ElementsOf([ducts]).OrderBy(element => element.Id).ToList();
            var prefix = $"{ThemeManager.Current.ActualApplicationTheme}-{(int)window.Width}";

            panel.ContextBar.Tick([FakeModelGenerator.Mep]);
            await Settle(800);
            var linkedDucts = panel.Explorer.Rows.First(row => row.Document == FakeModelGenerator.Mep && row.Text == "Ducts");
            var linked = panel.Explorer.ElementsOf([linkedDucts]).OrderBy(element => element.Id).First();

            host.Explainer.Reasons =
            [
                HiddenReasons.LinkElement,
                HiddenReasons.Filter("Walls-Demo"),
                HiddenReasons.WorksetClosed("Shell"),
                HiddenReasons.BelowViewDepth
            ];
            await OpenAsync(panel, linked);
            ShotRun.Save(window, Path.Combine(directory, $"{prefix}-why-hidden.png"));

            panel.WhyHidden.IsExpanded = true;
            await Settle(400);
            ShotRun.Save(window, Path.Combine(directory, $"{prefix}-why-hidden-expanded.png"));

            host.Explainer.Reasons = [HiddenReasons.Category("Duct Fittings"), HiddenReasons.OutsideCrop];
            await OpenAsync(panel, elements[0]);
            ShotRun.Save(window, Path.Combine(directory, $"{prefix}-why-hidden-category.png"));

            host.Explainer.Reasons = [];
            await OpenAsync(panel, elements[1]);
            ShotRun.Save(window, Path.Combine(directory, $"{prefix}-why-hidden-none.png"));
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
        finally
        {
            panel?.Dispose();
            app.Shutdown();
        }
    }

    private static async Task OpenAsync(ModelContextViewModel panel, ElementRef element)
    {
        await panel.WhyHidden.OpenAsync(element);
        await Settle(400);
    }

    private static Task Settle(int milliseconds)
    {
        return Task.Delay(milliseconds);
    }
}
