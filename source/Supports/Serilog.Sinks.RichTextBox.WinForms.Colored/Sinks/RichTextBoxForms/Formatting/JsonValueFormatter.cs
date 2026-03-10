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
using Serilog.Sinks.RichTextBoxForms.Extensions;
using Serilog.Sinks.RichTextBoxForms.Rtf;
using Serilog.Sinks.RichTextBoxForms.Themes;
using Serilog.Sinks.RichTextBoxForms.Tokens;
using System.Globalization;
using System.Text;

namespace Serilog.Sinks.RichTextBoxForms.Formatting;

public class JsonValueFormatter(RichTextBoxSinkOptions options) : ValueFormatter(options)
{
    private const int MaxRetainedBuilderCapacity = 16 * 1024;
    private readonly StringBuilder _literalBuilder = new();
    private readonly StringBuilder _scalarBuilder = new();
    private readonly StringBuilder _jsonStringBuilder = new();

    protected override ValueFormatterState CreateInitialState(IRtfCanvas canvas, string format, bool isLiteral)
    {
        return new ValueFormatterState(canvas, format, isLiteral, 0, Options.SpacesPerIndent);
    }

    protected override bool VisitScalarValue(ValueFormatterState state, ScalarValue scalar)
    {
        FormatLiteralValue(scalar, state.Canvas, state.IsLiteral);
        return true;
    }

    protected override bool VisitSequenceValue(ValueFormatterState state, SequenceValue sequence)
    {
        if (Options.PrettyPrintJson) RenderSequencePretty(state, sequence);
        else RenderSequenceCompact(state, sequence);
        return true;
    }

    protected override bool VisitStructureValue(ValueFormatterState state, StructureValue structure)
    {
        if (Options.PrettyPrintJson) RenderStructurePretty(state, structure);
        else RenderStructureCompact(state, structure);
        return true;
    }

    protected override bool VisitDictionaryValue(ValueFormatterState state, DictionaryValue dictionary)
    {
        if (Options.PrettyPrintJson) RenderDictionaryPretty(state, dictionary);
        else RenderDictionaryCompact(state, dictionary);
        return true;
    }

    private void FormatLiteralValue(ScalarValue scalar, IRtfCanvas canvas, bool isLiteral = false)
    {
        var value = scalar.Value;
        if (value is null)
        {
            Options.Theme.Render(canvas, StyleToken.Null, "null");
            return;
        }

        if (TryRenderTokenValue(canvas, value) ||
            TryRenderStringValue(canvas, value, isLiteral) ||
            TryRenderPrimitiveValue(canvas, value) ||
            TryRenderQuotedScalar(canvas, scalar, value) ||
            TryRenderNumericValueType(canvas, value))
        {
            return;
        }

        RenderScalarFallback(canvas, scalar);
    }

    private string GetQuotedJsonString(string str)
    {
        ClearBuilder(_jsonStringBuilder);
        _jsonStringBuilder.Append('\"');

        var segmentStart = 0;
        for (var i = 0; i < str.Length; i++)
        {
            var c = str[i];
            if (!NeedsJsonEscape(c))
            {
                continue;
            }

            AppendUnescapedSegment(str, segmentStart, i);
            AppendEscapedJsonChar(c);
            segmentStart = i + 1;
        }

        AppendUnescapedSegment(str, segmentStart, str.Length);

        _jsonStringBuilder.Append('\"');
        return _jsonStringBuilder.ToString();
    }

    private void RenderSequencePretty(ValueFormatterState state, SequenceValue sequence)
    {
        var indentState = state.ToIndentUp();
        if (sequence.Elements.Count > 0)
            Options.Theme.Render(state.Canvas, StyleToken.TertiaryText, "[\n" + indentState.GetIndentation());
        else
            Options.Theme.Render(state.Canvas, StyleToken.TertiaryText, "[");

        var delimiter = string.Empty;
        foreach (var propertyValue in sequence.Elements)
        {
            if (!string.IsNullOrEmpty(delimiter))
                Options.Theme.Render(state.Canvas, StyleToken.TertiaryText, delimiter);

            delimiter = ",\n" + indentState.GetIndentation();
            Visit(indentState, propertyValue);
        }

        if (sequence.Elements.Count > 0)
            Options.Theme.Render(state.Canvas, StyleToken.TertiaryText, "\n" + state.GetIndentation() + "]");
        else
            Options.Theme.Render(state.Canvas, StyleToken.TertiaryText, "]");
    }

    private void RenderSequenceCompact(ValueFormatterState state, SequenceValue sequence)
    {
        Options.Theme.Render(state.Canvas, StyleToken.TertiaryText, "[");
        var delimiter = string.Empty;
        foreach (var propertyValue in sequence.Elements)
        {
            if (!string.IsNullOrEmpty(delimiter))
                Options.Theme.Render(state.Canvas, StyleToken.TertiaryText, delimiter);

            delimiter = ", ";
            Visit(state, propertyValue);
        }

        Options.Theme.Render(state.Canvas, StyleToken.TertiaryText, "]");
    }

    private void RenderStructurePretty(ValueFormatterState state, StructureValue structure)
    {
        var indentState = state.ToIndentUp();
        var hasContent = structure.Properties.Count > 0 || structure.TypeTag != null;
        if (hasContent)
            Options.Theme.Render(state.Canvas, StyleToken.TertiaryText, "{\n" + indentState.GetIndentation());
        else
            Options.Theme.Render(state.Canvas, StyleToken.TertiaryText, "{");

        var delimiter = string.Empty;
        foreach (var eventProperty in structure.Properties)
        {
            if (!string.IsNullOrEmpty(delimiter))
                Options.Theme.Render(state.Canvas, StyleToken.TertiaryText, delimiter);

            delimiter = ",\n" + indentState.GetIndentation();
            Options.Theme.Render(state.Canvas, StyleToken.Name, GetQuotedJsonString(eventProperty.Name));
            Options.Theme.Render(state.Canvas, StyleToken.TertiaryText, ": ");
            Visit(indentState.Next(), eventProperty.Value);
        }

        RenderTypeTag(state, structure.TypeTag, delimiter);
        if (hasContent)
            Options.Theme.Render(state.Canvas, StyleToken.TertiaryText, "\n" + state.GetIndentation() + "}");
        else
            Options.Theme.Render(state.Canvas, StyleToken.TertiaryText, "}");
    }

    private void RenderStructureCompact(ValueFormatterState state, StructureValue structure)
    {
        Options.Theme.Render(state.Canvas, StyleToken.TertiaryText, "{");
        var delimiter = string.Empty;
        foreach (var eventProperty in structure.Properties)
        {
            if (!string.IsNullOrEmpty(delimiter))
                Options.Theme.Render(state.Canvas, StyleToken.TertiaryText, delimiter);

            delimiter = ", ";
            Options.Theme.Render(state.Canvas, StyleToken.Name, GetQuotedJsonString(eventProperty.Name));
            Options.Theme.Render(state.Canvas, StyleToken.TertiaryText, ": ");
            Visit(state.Next(), eventProperty.Value);
        }

        RenderTypeTag(state, structure.TypeTag, delimiter);
        Options.Theme.Render(state.Canvas, StyleToken.TertiaryText, "}");
    }

    private void RenderDictionaryPretty(ValueFormatterState state, DictionaryValue dictionary)
    {
        var indentState = state.ToIndentUp();
        if (dictionary.Elements.Count > 0)
            Options.Theme.Render(state.Canvas, StyleToken.TertiaryText, "{\n" + indentState.GetIndentation());
        else
            Options.Theme.Render(state.Canvas, StyleToken.TertiaryText, "{");

        var delimiter = string.Empty;
        foreach (var (scalar, propertyValue) in dictionary.Elements)
        {
            if (!string.IsNullOrEmpty(delimiter))
                Options.Theme.Render(state.Canvas, StyleToken.TertiaryText, delimiter);

            delimiter = ",\n" + indentState.GetIndentation();
            RenderDictionaryKey(state.Canvas, scalar);
            Options.Theme.Render(state.Canvas, StyleToken.TertiaryText, ": ");
            Visit(indentState.Next(), propertyValue);
        }

        if (dictionary.Elements.Count > 0)
            Options.Theme.Render(state.Canvas, StyleToken.TertiaryText, "\n" + state.GetIndentation() + "}");
        else
            Options.Theme.Render(state.Canvas, StyleToken.TertiaryText, "}");
    }

    private void RenderDictionaryCompact(ValueFormatterState state, DictionaryValue dictionary)
    {
        Options.Theme.Render(state.Canvas, StyleToken.TertiaryText, "{");
        var delimiter = string.Empty;
        foreach (var (scalar, propertyValue) in dictionary.Elements)
        {
            if (!string.IsNullOrEmpty(delimiter))
                Options.Theme.Render(state.Canvas, StyleToken.TertiaryText, delimiter);

            delimiter = ", ";
            RenderDictionaryKey(state.Canvas, scalar);
            Options.Theme.Render(state.Canvas, StyleToken.TertiaryText, ": ");
            Visit(state.Next(), propertyValue);
        }

        Options.Theme.Render(state.Canvas, StyleToken.TertiaryText, "}");
    }

    private void RenderTypeTag(ValueFormatterState state, string? typeTag, string delimiter)
    {
        if (typeTag == null)
        {
            return;
        }

        Options.Theme.Render(state.Canvas, StyleToken.TertiaryText, delimiter);
        Options.Theme.Render(state.Canvas, StyleToken.Name, GetQuotedJsonString("$type"));
        Options.Theme.Render(state.Canvas, StyleToken.TertiaryText, ": ");
        Options.Theme.Render(state.Canvas, StyleToken.String, GetQuotedJsonString(typeTag));
    }

    private void RenderDictionaryKey(IRtfCanvas canvas, ScalarValue scalar)
    {
        var style = scalar.Value switch
        {
            null => StyleToken.Null,
            string => StyleToken.String,
            _ => StyleToken.Scalar
        };
        Options.Theme.Render(canvas, style, GetQuotedJsonString(scalar.Value?.ToString() ?? "null"));
    }

    private bool TryRenderTokenValue(IRtfCanvas canvas, object value)
    {
        if (value is string)
        {
            return false;
        }

        if (!Options.TokenDetector.TryCreateToken(value, out var token))
        {
            return false;
        }

        TokenRenderHelper.RenderObject(canvas, Options, StyleToken.Number, value, token.NormalizedValue);
        return true;
    }

    private bool TryRenderStringValue(IRtfCanvas canvas, object value, bool isLiteral)
    {
        if (value is not string str)
        {
            return false;
        }

        var display = isLiteral ? str : GetQuotedJsonString(str);
        TokenRenderHelper.RenderString(canvas, Options, StyleToken.String, str, display);
        return true;
    }

    private bool TryRenderPrimitiveValue(IRtfCanvas canvas, object value)
    {
        switch (value)
        {
            case byte[] bytes:
                Options.Theme.Render(canvas, StyleToken.String, GetQuotedJsonString(Convert.ToBase64String(bytes)));
                return true;
            case bool b:
                Options.Theme.Render(canvas, StyleToken.Boolean, b ? "true" : "false");
                return true;
            case double d:
                RenderDoubleValue(canvas, d);
                return true;
            case float f:
                RenderFloatValue(canvas, f);
                return true;
            default:
                return false;
        }
    }

    private bool TryRenderQuotedScalar(IRtfCanvas canvas, ScalarValue scalar, object value)
    {
        switch (value)
        {
            case char:
            case DateTime:
            case DateTimeOffset:
            case TimeSpan:
            case Guid:
            case Uri:
                ClearBuilder(_literalBuilder);
                using (var writer = new StringWriter(_literalBuilder))
                {
                    if (value is DateTime dt)
                        writer.Write(dt.ToString("O", CultureInfo.InvariantCulture));
                    else if (value is DateTimeOffset dto)
                        writer.Write(dto.ToString("O", CultureInfo.InvariantCulture));
                    else
                        scalar.Render(writer, null, Options.FormatProvider);
                }

                Options.Theme.Render(canvas, StyleToken.Scalar, GetQuotedJsonString(_literalBuilder.ToString()));
                return true;
            default:
                return false;
        }
    }

    private void RenderDoubleValue(IRtfCanvas canvas, double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            Options.Theme.Render(canvas, StyleToken.Number, GetQuotedJsonString(value.ToString(CultureInfo.InvariantCulture)));
        else
            Options.Theme.Render(canvas, StyleToken.Number, value.ToString("R", CultureInfo.InvariantCulture));
    }

    private void RenderFloatValue(IRtfCanvas canvas, float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            Options.Theme.Render(canvas, StyleToken.Number, GetQuotedJsonString(value.ToString(CultureInfo.InvariantCulture)));
        else
            Options.Theme.Render(canvas, StyleToken.Number, value.ToString("R", CultureInfo.InvariantCulture));
    }

    private bool TryRenderNumericValueType(IRtfCanvas canvas, object value)
    {
        if (value is not ValueType or not (int or uint or long or ulong or decimal or byte or sbyte or short or ushort))
        {
            return false;
        }

        Options.Theme.Render(canvas, StyleToken.Number, ((IFormattable)value).ToString(null, CultureInfo.InvariantCulture));
        return true;
    }

    private void RenderScalarFallback(IRtfCanvas canvas, ScalarValue scalar)
    {
        if (scalar.Value is IFormattable formattable)
        {
            RenderFormattable(canvas, formattable, null);
            return;
        }

        ClearBuilder(_scalarBuilder);
        using (var writer = new StringWriter(_scalarBuilder))
        {
            scalar.Render(writer, null, Options.FormatProvider);
        }

        Options.Theme.Render(canvas, StyleToken.Scalar, GetQuotedJsonString(_scalarBuilder.ToString()));
    }

    private static bool NeedsJsonEscape(char c)
    {
        return c is < (char)32 or '\\' or '"';
    }

    private void AppendUnescapedSegment(string value, int segmentStart, int segmentEndExclusive)
    {
        var length = segmentEndExclusive - segmentStart;
        if (length > 0)
        {
            _jsonStringBuilder.Append(value, segmentStart, length);
        }
    }

    private void AppendEscapedJsonChar(char c)
    {
        switch (c)
        {
            case '"':
                _jsonStringBuilder.Append("\\\"");
                break;
            case '\\':
                _jsonStringBuilder.Append("\\\\");
                break;
            case '\n':
                _jsonStringBuilder.Append("\\n");
                break;
            case '\r':
                _jsonStringBuilder.Append("\\r");
                break;
            case '\f':
                _jsonStringBuilder.Append("\\f");
                break;
            case '\t':
                _jsonStringBuilder.Append("\\t");
                break;
            default:
                _jsonStringBuilder.Append("\\u");
                _jsonStringBuilder.Append(((int)c).ToString("X4"));
                break;
        }
    }

    private static void ClearBuilder(StringBuilder builder)
    {
        builder.Clear();
        if (builder.Capacity > MaxRetainedBuilderCapacity)
        {
            builder.Capacity = MaxRetainedBuilderCapacity;
        }
    }
}