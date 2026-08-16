using System.Text;
using DevTools.UI;
using RevitDevTool.Logging.Enums;
using ZLogger.Scintilla.Models;
using ZLogger.Scintilla.Public;
// ReSharper disable ForCanBeConvertedToForeach

namespace RevitDevTool.Logging.Linkify;

internal sealed class RevitLinkifier : ILinkifier
{
    private const string ElementIdFullName = "Autodesk.Revit.DB.ElementId";

    public bool TryMatch(ReadOnlySpan<byte> utf8Token, RenderContext context, out Action? onClick)
    {
        onClick = null;

        if (utf8Token.IsEmpty) return false;

        var tokenText = Encoding.UTF8.GetString(utf8Token.ToArray());

        if (ParameterSpan.HasParameter(context, ElementIdFullName, tokenText))
        {
            onClick = CreateSearchAction(RevitTokenKind.ElementId, tokenText);
            return true;
        }

        switch (utf8Token.Length)
        {
            case 45 when IsRevitUniqueId(utf8Token):
                onClick = CreateSearchAction(RevitTokenKind.UniqueId, tokenText);
                return true;
            case 22 when IsRevitIfcGuid(utf8Token):
                onClick = CreateSearchAction(RevitTokenKind.IfcGuid, tokenText);
                return true;
            default:
                return false;
        }
    }

    private static Action CreateSearchAction(RevitTokenKind tokenKind, string value)
    {
        return () => HostUiHelper.RunOnMainThread(
            () => RevitSearchService.TrySearchAndSelectInActiveDocument(tokenKind, value));
    }

    private static bool IsRevitUniqueId(ReadOnlySpan<byte> span)
    {
        return span[8] == (byte)'-'
            && span[13] == (byte)'-'
            && span[18] == (byte)'-'
            && span[23] == (byte)'-'
            && span[36] == (byte)'-'
            && IsHexSegment(span, 0, 8)
            && IsHexSegment(span, 9, 4)
            && IsHexSegment(span, 14, 4)
            && IsHexSegment(span, 19, 4)
            && IsHexSegment(span, 24, 12)
            && IsHexSegment(span, 37, 8);
    }

    private static bool IsHexSegment(ReadOnlySpan<byte> span, int start, int length)
    {
        for (var i = 0; i < length; i++)
        {
            var c = span[start + i];
            var isHex = c is (>= 0x30 and <= 0x39)
                          or (>= 0x61 and <= 0x66)
                          or (>= 0x41 and <= 0x46);
            if (!isHex) return false;
        }
        return true;
    }

    private static bool IsRevitIfcGuid(ReadOnlySpan<byte> span)
    {
        if (span[0] is not (0x30 or 0x31 or 0x32 or 0x33)) return false;

        var hasDigitOrSpecial = false;
        for (var i = 0; i < span.Length; i++)
        {
            var c = span[i];
            switch (c)
            {
                case >= 0x30 and <= 0x39: // 0-9
                case 0x5F:               // _
                case 0x24:               // $
                    hasDigitOrSpecial = true;
                    break;
                case >= 0x41 and <= 0x5A: // A-Z
                case >= 0x61 and <= 0x7A: // a-z
                    break;
                default:
                    return false;
            }
        }

        return hasDigitOrSpecial;
    }
}
