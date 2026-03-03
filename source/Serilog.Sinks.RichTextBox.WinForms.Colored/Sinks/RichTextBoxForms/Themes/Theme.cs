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

using Serilog.Sinks.RichTextBoxForms.Rtf;

namespace Serilog.Sinks.RichTextBoxForms.Themes;

public class Theme(Style defaultStyle, Dictionary<StyleToken, Style> styles)
{
    public Style DefaultStyle { get; } = defaultStyle;

    public bool IsDarkTheme { get; } = defaultStyle.Background.GetBrightness() < 0.5f;

    public IEnumerable<Color> Colors
    {
        get
        {
            yield return DefaultStyle.Foreground;
            yield return DefaultStyle.Background;

            foreach (var style in styles.Values)
            {
                yield return style.Foreground;
                yield return style.Background;
            }
        }
    }

    public void Render(IRtfCanvas canvas, StyleToken styleToken, string value)
    {
        var themeStyle = GetStyle(styleToken);

        canvas.SelectionStart = canvas.TextLength;
        canvas.SelectionLength = 0;
        canvas.SelectionColor = themeStyle.Foreground;
        canvas.SelectionBackColor = themeStyle.Background;
        canvas.AppendText(value);

        canvas.SelectionColor = DefaultStyle.Foreground;
        canvas.SelectionBackColor = DefaultStyle.Background;
    }

    public Style GetStyle(StyleToken styleToken)
    {
        return styles[styleToken];
    }
}