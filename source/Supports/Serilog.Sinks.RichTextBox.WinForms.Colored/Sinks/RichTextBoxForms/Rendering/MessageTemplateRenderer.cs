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

using Serilog.Events;
using Serilog.Parsing;
using Serilog.Sinks.RichTextBoxForms.Formatting;
using Serilog.Sinks.RichTextBoxForms.Rtf;
using Serilog.Sinks.RichTextBoxForms.Themes;
using Serilog.Sinks.RichTextBoxForms.Tokens;

namespace Serilog.Sinks.RichTextBoxForms.Rendering;

public class MessageTemplateRenderer(RichTextBoxSinkOptions options, ValueFormatter valueFormatter, bool isLiteral)
{
    private readonly RichTextBoxSinkOptions _options = options ?? throw new ArgumentNullException(nameof(options));

    public void Render(MessageTemplate template, IReadOnlyDictionary<string, LogEventPropertyValue> properties, IRtfCanvas canvas)
    {
        foreach (var token in template.Tokens)
        {
            if (token is TextToken textToken)
            {
                TokenRenderHelper.RenderString(canvas, _options, StyleToken.Text, textToken.Text, textToken.Text);
            }
            else
            {
                RenderPropertyToken((PropertyToken)token, properties, canvas);
            }
        }
    }

    private void RenderPropertyToken(PropertyToken propertyToken, IReadOnlyDictionary<string, LogEventPropertyValue> properties, IRtfCanvas canvas)
    {
        if (!properties.TryGetValue(propertyToken.PropertyName, out var propertyValue))
        {
            _options.Theme.Render(canvas, StyleToken.Invalid, propertyToken.ToString());
            return;
        }

        RenderValue(propertyValue, canvas, propertyToken.Format ?? "");
    }

    private void RenderValue(LogEventPropertyValue propertyValue, IRtfCanvas canvas, string format)
    {
        valueFormatter.Format(propertyValue, canvas, format, isLiteral);
    }
}