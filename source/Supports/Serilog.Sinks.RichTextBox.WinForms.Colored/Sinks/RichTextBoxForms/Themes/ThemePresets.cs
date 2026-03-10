#region Copyright 2025 Simon Vonhoff & Contributors

//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//

#endregion

namespace Serilog.Sinks.RichTextBoxForms.Themes;

public static class ThemePresets
{
    private static readonly Color LightBackground = Color.FromArgb(250, 250, 250);
    private static readonly Color DarkBackground = Color.FromArgb(37, 37, 37);

    public static Theme EnhancedDark { get; } = new(
        new Style(Color.FromArgb(240, 240, 240), DarkBackground),
        new Dictionary<StyleToken, Style>
        {
            [StyleToken.Text] = new(Color.FromArgb(240, 240, 240), DarkBackground),
            [StyleToken.SecondaryText] = new(Color.FromArgb(180, 180, 180), DarkBackground),
            [StyleToken.TertiaryText] = new(Color.FromArgb(160, 160, 160), DarkBackground),
            [StyleToken.Invalid] = new(Color.FromArgb(255, 220, 120), DarkBackground),
            [StyleToken.Null] = new(Color.FromArgb(180, 180, 255), DarkBackground),
            [StyleToken.Name] = new(Color.FromArgb(255, 200, 255), DarkBackground),
            [StyleToken.String] = new(Color.FromArgb(120, 255, 170), DarkBackground),
            [StyleToken.Number] = new(Color.FromArgb(255, 200, 120), DarkBackground),
            [StyleToken.Boolean] = new(Color.FromArgb(170, 220, 255), DarkBackground),
            [StyleToken.Scalar] = new(Color.FromArgb(170, 255, 220), DarkBackground),
            [StyleToken.LevelVerbose] = new(Color.FromArgb(140, 140, 140), DarkBackground),
            [StyleToken.LevelDebug] = new(Color.FromArgb(170, 170, 170), DarkBackground),
            [StyleToken.LevelInformation] = new(Color.FromArgb(120, 170, 255), DarkBackground),
            [StyleToken.LevelWarning] = new(Color.FromArgb(255, 220, 70), DarkBackground),
            [StyleToken.LevelError] = new(Color.FromArgb(255, 100, 100), DarkBackground),
            [StyleToken.LevelFatal] = new(Color.FromArgb(255, 80, 80), DarkBackground)
        });

    public static Theme EnhancedLight { get; } = new(
        new Style(Color.FromArgb(40, 40, 40), LightBackground),
        new Dictionary<StyleToken, Style>
        {
            [StyleToken.Text] = new(Color.FromArgb(40, 40, 40), LightBackground),
            [StyleToken.SecondaryText] = new(Color.FromArgb(80, 80, 80), LightBackground),
            [StyleToken.TertiaryText] = new(Color.FromArgb(120, 120, 120), LightBackground),
            [StyleToken.Invalid] = new(Color.FromArgb(180, 80, 0), LightBackground),
            [StyleToken.Null] = new(Color.FromArgb(80, 80, 200), LightBackground),
            [StyleToken.Name] = new(Color.FromArgb(150, 0, 150), LightBackground),
            [StyleToken.String] = new(Color.FromArgb(0, 120, 0), LightBackground),
            [StyleToken.Number] = new(Color.FromArgb(180, 80, 0), LightBackground),
            [StyleToken.Boolean] = new(Color.FromArgb(0, 80, 180), LightBackground),
            [StyleToken.Scalar] = new(Color.FromArgb(0, 140, 100), LightBackground),
            [StyleToken.LevelVerbose] = new(Color.FromArgb(120, 120, 120), LightBackground),
            [StyleToken.LevelDebug] = new(Color.FromArgb(80, 80, 80), LightBackground),
            [StyleToken.LevelInformation] = new(Color.FromArgb(0, 80, 180), LightBackground),
            [StyleToken.LevelWarning] = new(Color.FromArgb(200, 140, 0), LightBackground),
            [StyleToken.LevelError] = new(Color.FromArgb(200, 60, 60), LightBackground),
            [StyleToken.LevelFatal] = new(Color.FromArgb(150, 30, 30), LightBackground)
        });

    /// <summary>
    /// Soft dark theme with muted colors for extended use.
    /// </summary>
    public static Theme SoftDark { get; } = new(
        new Style(Color.FromArgb(200, 200, 200), DarkBackground),
        new Dictionary<StyleToken, Style>
        {
            [StyleToken.Text] = new(Color.FromArgb(200, 200, 200), DarkBackground),
            [StyleToken.SecondaryText] = new(Color.FromArgb(150, 150, 150), DarkBackground),
            [StyleToken.TertiaryText] = new(Color.FromArgb(120, 120, 120), DarkBackground),
            [StyleToken.Invalid] = new(Color.FromArgb(220, 180, 80), DarkBackground),
            [StyleToken.Null] = new(Color.FromArgb(130, 130, 220), DarkBackground),
            [StyleToken.Name] = new(Color.FromArgb(220, 160, 220), DarkBackground),
            [StyleToken.String] = new(Color.FromArgb(120, 200, 140), DarkBackground),
            [StyleToken.Number] = new(Color.FromArgb(220, 160, 100), DarkBackground),
            [StyleToken.Boolean] = new(Color.FromArgb(130, 180, 220), DarkBackground),
            [StyleToken.Scalar] = new(Color.FromArgb(140, 200, 180), DarkBackground),
            [StyleToken.LevelVerbose] = new(Color.FromArgb(100, 100, 100), DarkBackground),
            [StyleToken.LevelDebug] = new(Color.FromArgb(130, 130, 130), DarkBackground),
            [StyleToken.LevelInformation] = new(Color.FromArgb(120, 160, 220), DarkBackground),
            [StyleToken.LevelWarning] = new(Color.FromArgb(220, 180, 60), DarkBackground),
            [StyleToken.LevelError] = new(Color.FromArgb(180, 60, 60), DarkBackground),
            [StyleToken.LevelFatal] = new(Color.FromArgb(150, 40, 40), DarkBackground)
        });

    /// <summary>
    /// High contrast light theme for better accessibility.
    /// </summary>
    public static Theme HighContrastLight { get; } = new(
        new Style(Color.FromArgb(20, 20, 20), LightBackground),
        new Dictionary<StyleToken, Style>
        {
            [StyleToken.Text] = new(Color.FromArgb(20, 20, 20), LightBackground),
            [StyleToken.SecondaryText] = new(Color.FromArgb(60, 60, 60), LightBackground),
            [StyleToken.TertiaryText] = new(Color.FromArgb(100, 100, 100), LightBackground),
            [StyleToken.Invalid] = new(Color.FromArgb(180, 80, 0), LightBackground),
            [StyleToken.Null] = new(Color.FromArgb(60, 60, 180), LightBackground),
            [StyleToken.Name] = new(Color.FromArgb(120, 0, 120), LightBackground),
            [StyleToken.String] = new(Color.FromArgb(0, 100, 0), LightBackground),
            [StyleToken.Number] = new(Color.FromArgb(160, 60, 0), LightBackground),
            [StyleToken.Boolean] = new(Color.FromArgb(0, 60, 160), LightBackground),
            [StyleToken.Scalar] = new(Color.FromArgb(0, 120, 80), LightBackground),
            [StyleToken.LevelVerbose] = new(Color.FromArgb(100, 100, 100), LightBackground),
            [StyleToken.LevelDebug] = new(Color.FromArgb(60, 60, 60), LightBackground),
            [StyleToken.LevelInformation] = new(Color.FromArgb(0, 60, 160), LightBackground),
            [StyleToken.LevelWarning] = new(Color.FromArgb(255, 200, 80), LightBackground),
            [StyleToken.LevelError] = new(Color.FromArgb(180, 40, 40), LightBackground),
            [StyleToken.LevelFatal] = new(Color.FromArgb(120, 20, 20), LightBackground)
        });
}