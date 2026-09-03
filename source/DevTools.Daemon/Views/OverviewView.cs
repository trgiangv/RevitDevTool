using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using DevTools.Daemon.Desktop;

namespace DevTools.Daemon.Views;

public sealed class OverviewView(AppState state) : UserControl
{
    protected override Element OnBuild()
    {
        var signedIn = new StackPanel()
            .Horizontal()
            .Spacing(12)
            .Margin(0, 0, 0, 8)
            .BindIsVisible(state.IsAuthenticated)
            .Children(
                new Border
                    {
                        Width = 48,
                        Height = 48,
                        CornerRadius = 24,
                        ClipToBounds = true,
                    }
                    .Child(
                        new Image().Bind(Image.SourceProperty, state.AvatarImage)),
                new StackPanel()
                    .Vertical()
                    .CenterVertical()
                    .Children(
                        new TextBlock()
                            .Bold()
                            .BindText(state.DisplayName),
                        new TextBlock()
                            .FontSize(ThemeFontSize.Small)
                            .WithTheme((theme, block) => block.Foreground(theme.Palette.PlaceholderText))
                            .BindText(state.Email)));

        var signedOut = new StackPanel()
            .Vertical()
            .Margin(0, 0, 0, 8)
            .BindIsVisible(state.IsAuthenticated, value => !value)
            .Children(
                new TextBlock()
                    .Text("Not signed in")
                    .Margin(0, 0, 0, 8),
                new Button()
                    .Content("Sign In")
                    .Left()
                    .OnClick(() => _ = state.SignIn()));

        return new StackPanel()
            .Vertical()
            .Margin(16)
            .Children(
                Heading("Account"),
                signedIn,
                signedOut,
                Heading("Gateway", top: 24),
                new TextBlock().BindText(state.GatewayStatus),
                Heading("Connected Hosts", top: 24),
                new TextBlock().BindText(state.Hosts.Count, count => $"{count} host(s)"));
    }

    private static TextBlock Heading(string text, double top = 0) =>
        new TextBlock()
            .Text(text)
            .FontSize(16)
            .Bold()
            .Margin(0, top, 0, 8);
}
