using System.Text;
using DevTools.UI;
using RevitDevTool.Core;
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

        if (utf8Token.IsEmpty)
            return false;

        var tokenText = Encoding.UTF8.GetString(utf8Token.ToArray());

        if (LinkToken.TrySplit(tokenText, out var linkInstanceId, out var inner)
            && LinkToken.TryClassifyInner(inner.AsSpan(), out var scopedKind))
        {
            onClick = CreateSearchAction(scopedKind, inner, linkInstanceId);
            return true;
        }

        if (ParameterSpan.HasParameter(context, ElementIdFullName, tokenText))
        {
            onClick = CreateSearchAction(RevitTokenKind.ElementId, tokenText, linkInstanceId: null);
            return true;
        }

        var tokenChars = tokenText.AsSpan();
        if (LinkToken.IsUniqueId(tokenChars))
        {
            onClick = CreateSearchAction(RevitTokenKind.UniqueId, tokenText, linkInstanceId: null);
            return true;
        }

        if (LinkToken.IsIfcGuid(tokenChars))
        {
            onClick = CreateSearchAction(RevitTokenKind.IfcGuid, tokenText, linkInstanceId: null);
            return true;
        }

        return false;
    }

    private static Action CreateSearchAction(RevitTokenKind tokenKind, string value, string? linkInstanceId)
    {
        return () => HostUiHelper.RunOnMainThread(() =>
        {
            try
            {
                var uiDocument = RevitContext.UiApplication.ActiveUIDocument;
                if (uiDocument?.Document is null)
                    return;

                var match = ElementSearcher.TrySearch(uiDocument.Document, tokenKind, value, linkInstanceId);
                if (match is not null)
                    ElementSelector.Select(uiDocument, match);
            }
            catch
            {
                // Monitor click is silent on miss or API failure.
            }
        });
    }
}
