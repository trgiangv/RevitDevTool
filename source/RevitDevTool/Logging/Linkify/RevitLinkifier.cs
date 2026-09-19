using System.Text;
using DevTools.UI;
using RevitDevTool.Tools.Selection;
using RevitDevTool.Core;
using ZLogger.Scintilla.Models;
using ZLogger.Scintilla.Public;
// ReSharper disable ForCanBeConvertedToForeach

namespace RevitDevTool.Logging.Linkify;

internal sealed class RevitLinkifier : ILinkifier
{
    public bool TryMatch(ReadOnlySpan<byte> utf8Token, RenderContext context, out Action? onClick)
    {
        onClick = null;

        if (utf8Token.IsEmpty)
            return false;

        var tokenText = Encoding.UTF8.GetString(utf8Token.ToArray());
        if (TokenParser.TryParse(tokenText) is not { } parsed)
            return false;
        
        if (parsed is { LinkInstanceId: null, Kind: TokenKind.ElementId }
            && !ParameterSpan.HasParameter(context, typeof(ElementId).FullName!, tokenText))
            return false;

        onClick = CreateSearchAction(parsed);
        return true;
    }

    private static Action CreateSearchAction(TokenParser.ParsedToken parsed)
    {
        return () => HostUiHelper.RunOnMainThread(() =>
        {
            try
            {
                var uiDocument = RevitContext.ActiveUiDocument;
                if (uiDocument is null)
                    return;

                var match = ElementSearcher.TrySearch(
                    uiDocument.Document,
                    parsed.Kind,
                    parsed.Value,
                    parsed.LinkInstanceId);
                if (match is not null)
                    ElementSelector.Select([match], zoom: true, sectionBox: false);
            }
            catch
            {
                // Monitor click is silent on miss or API failure.
            }
        });
    }
}
