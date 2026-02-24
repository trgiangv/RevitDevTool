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
using Serilog.Sinks.RichTextBoxForms.Themes;

namespace Serilog.Sinks.RichTextBoxForms.Tokens;

internal static class TokenRenderHelper
{
    private const StyleToken LinkStyleToken = StyleToken.LevelInformation;

    internal static void RenderObject(IRtfCanvas canvas, RichTextBoxSinkOptions options, StyleToken styleToken, object? rawValue, string renderedText)
    {
        if (!options.EnableTokenLinks)
        {
            options.Theme.Render(canvas, styleToken, renderedText);
            return;
        }

        if (TryCreateToken(rawValue, renderedText, out var token) &&
            RevitTokenParser.TryBuildUri(token, out var uri))
        {
            AppendStyledHyperlink(canvas, options.Theme, LinkStyleToken, renderedText, uri);
            return;
        }

        options.Theme.Render(canvas, styleToken, renderedText);
    }

    internal static void RenderString(IRtfCanvas canvas, RichTextBoxSinkOptions options, StyleToken styleToken, string value, string displayText)
    {
        if (!options.EnableTokenLinks)
        {
            options.Theme.Render(canvas, styleToken, displayText);
            return;
        }

        if (RevitTokenParser.TryParseTokenString(value, out var kind, out var normalized))
        {
            var token = new DetectedToken(kind, value, normalized, value);
            if (RevitTokenParser.TryBuildUri(token, out var uri))
            {
                AppendStyledHyperlink(canvas, options.Theme, LinkStyleToken, value, uri);
                return;
            }
        }

        options.Theme.Render(canvas, styleToken, displayText);
    }

    private static bool TryCreateToken(object? rawValue, string displayText, out DetectedToken? token)
    {
        if (rawValue is null)
        {
            token = null;
            return false;
        }

        if (RevitTokenParser.TryParseElementIdObject(rawValue, out var elementId))
        {
            token = new DetectedToken(RevitTokenKind.ElementId, elementId, elementId, displayText);
            return true;
        }

        if (rawValue is string str &&
            RevitTokenParser.TryParseTokenString(str, out var kind, out var normalized))
        {
            token = new DetectedToken(kind, str, normalized, displayText);
            return true;
        }

        token = null;
        return false;
    }

    private static void AppendStyledHyperlink(IRtfCanvas canvas, Theme theme, StyleToken styleToken, string displayText, string uri)
    {
        var style = theme.GetStyle(styleToken);
        canvas.SelectionStart = canvas.TextLength;
        canvas.SelectionLength = 0;
        canvas.SelectionColor = style.Foreground;
        canvas.SelectionBackColor = style.Background;
        canvas.AppendHyperlink(displayText, uri);

        canvas.SelectionColor = theme.DefaultStyle.Foreground;
        canvas.SelectionBackColor = theme.DefaultStyle.Background;
    }
}