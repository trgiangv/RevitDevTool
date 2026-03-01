using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using RevitDevTool.Scintilla.Core;
using RevitDevTool.Scintilla.Formatting;

namespace RevitDevTool.Scintilla.Demo;

public sealed class DemoEnrichmentCallbacks : ILogEnrichmentCallbacks
{
    public bool EnablePrettyJson { get; set; } = true;
    public bool EnableTokenResolution { get; set; } = true;

    public bool ShouldPrettyPrint(in LogRenderContext context)
    {
        if (!EnablePrettyJson)
            return false;

        if (HasCallbackDrivenContent(context))
            return false;

        if (ContainsStructuredPayload(context.Properties))
            return true;

        if (string.IsNullOrWhiteSpace(context.Message))
            return false;

        var message = context.Message!.TrimStart();
        return message.StartsWith("{", StringComparison.Ordinal) || message.StartsWith("[", StringComparison.Ordinal);
    }

    public bool TryGetStructuredPayload(in LogRenderContext context, out ReadOnlyMemory<byte> utf8Json)
    {
        utf8Json = default;
        return false;
    }

    public bool TryResolveToken(in TokenCandidateContext candidate, out TokenResolution resolution)
    {
        resolution = default;
        if (!EnableTokenResolution || string.IsNullOrWhiteSpace(candidate.CandidateText))
            return false;

        var token = candidate.CandidateText.Trim();
        if (!TryResolveRevitToken(token, candidate.RenderContext.Properties, out var payload))
            return false;

        resolution = new TokenResolution(
            candidate.Utf16Start,
            candidate.Utf16Length,
            payload,
            isLink: true,
            LogSemanticStyle.TokenLink);
        return true;
    }

    public void OnTokenResolved(in TokenResolvedContext resolved)
    {
    }

    public void OnTokenClick(in TokenClickContext click)
    {
        if (TryGetTargetUri(click.Payload, out var targetUri))
        {
            Debug.WriteLine($"Callback token click => {targetUri}");
            var message = $"Token clicked: {targetUri}";
            System.Windows.Forms.MessageBox.Show(
                message,
                "Revit Token Callback",
                System.Windows.Forms.MessageBoxButtons.OK,
                System.Windows.Forms.MessageBoxIcon.Information);
        }
    }

    private static bool TryGetTargetUri(ILogTokenPayload payload, out string targetUri)
    {
        if (payload is DemoTokenPayload demoPayload && !string.IsNullOrWhiteSpace(demoPayload.TargetUri))
        {
            targetUri = demoPayload.TargetUri;
            return true;
        }

        targetUri = string.Empty;
        return false;
    }

    private static bool TryResolveRevitToken(
        string token,
        IReadOnlyDictionary<string, object?> properties,
        out DemoTokenPayload payload)
    {
        payload = default!;

        if (TryParseElementIdToken(token, properties, out var elementId))
        {
            payload = new DemoTokenPayload("ElementId", token, elementId, $"revitlog://elementid/{elementId}");
            return true;
        }

        if (LooksLikeUniqueId(token))
        {
            payload = new DemoTokenPayload("UniqueId", token, token, $"revitlog://uniqueid/{Uri.EscapeDataString(token)}");
            return true;
        }

        if (LooksLikeIfcGuid(token))
        {
            payload = new DemoTokenPayload("IfcGuid", token, token, $"revitlog://ifcguid/{Uri.EscapeDataString(token)}");
            return true;
        }

        return false;
    }

    private static bool TryParseElementIdToken(
        string token,
        IReadOnlyDictionary<string, object?> properties,
        out string elementId)
    {
        elementId = string.Empty;
        foreach (var pair in properties)
        {
            if (pair.Value is null)
                continue;

            if (!TryReadElementIdValue(pair.Value, out var value))
                continue;

            var candidate = value.ToString(CultureInfo.InvariantCulture);
            if (string.Equals(candidate, token, StringComparison.Ordinal))
            {
                elementId = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool TryReadElementIdValue(object value, out long elementId)
    {
        elementId = 0;
        var type = value.GetType();
        if (!string.Equals(type.Name, "ElementId", StringComparison.Ordinal))
            return false;

        if (!string.Equals(type.Namespace, "Autodesk.Revit.DB", StringComparison.Ordinal))
            return false;

        var intProp = type.GetProperty("IntegerValue", BindingFlags.Instance | BindingFlags.Public);
        if (intProp?.GetValue(value) is int intValue)
        {
            elementId = intValue;
            return true;
        }

        var longProp = type.GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);
        if (longProp?.GetValue(value) is long longValue)
        {
            elementId = longValue;
            return true;
        }

        return false;
    }

    private static bool LooksLikeUniqueId(string token)
        => token.Length == 45 && CountChar(token, '-') == 5;

    private static bool LooksLikeIfcGuid(string token)
    {
        if (token.Length != 22)
            return false;

        for (var i = 0; i < token.Length; i++)
        {
            var c = token[i];
            if (!(char.IsLetterOrDigit(c) || c == '_' || c == '$'))
                return false;
        }

        return true;
    }

    private static int CountChar(string text, char value)
    {
        var count = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == value)
                count++;
        }

        return count;
    }

    private static bool ContainsStructuredPayload(IReadOnlyDictionary<string, object?> properties)
    {
        if (properties is null || properties.Count == 0)
            return false;

        foreach (var pair in properties)
        {
            if (IsStructuredPayloadCandidate(pair.Value))
                return true;
        }

        return false;
    }

    private static bool HasCallbackDrivenContent(in LogRenderContext context)
    {
        if (!string.IsNullOrWhiteSpace(context.Message))
        {
            var message = context.Message!;
            if (message.IndexOf("elementId=", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("ifcGuid=", StringComparison.OrdinalIgnoreCase) >= 0 ||
                message.IndexOf("uniqueId=", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        foreach (var pair in context.Properties)
        {
            if (pair.Value is null)
                continue;

            var type = pair.Value.GetType();
            if (string.Equals(type.Name, "ElementId", StringComparison.Ordinal) &&
                string.Equals(type.Namespace, "Autodesk.Revit.DB", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsStructuredPayloadCandidate(object? value)
    {
        if (value is null || value is string || value is Exception)
            return false;

        if (value is byte[] || value is ArraySegment<byte> || value is ReadOnlyMemory<byte> || value is Memory<byte>)
            return false;

        if (value is System.Collections.IDictionary)
            return false;

        var type = value.GetType();
        if (type.IsPrimitive || type.IsEnum)
            return false;

        if (value is decimal or DateTime or DateTimeOffset or TimeSpan or Guid)
            return false;

        return true;
    }

}
