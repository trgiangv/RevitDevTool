using System.Text;
using RevitDevTool.Scintilla.Core;
using RevitDevTool.Scintilla.Formatting;
namespace RevitDevTool.Scintilla.Services;

internal sealed class ScintillaInteractionService
{
    private readonly ScintillaNET.Scintilla _scintilla;
    private readonly ScintillaLogViewerOptions _options;
    private readonly List<TokenRange> _tokenRanges;
    private bool _isHoveringLink;

    public ScintillaInteractionService(
        ScintillaNET.Scintilla scintilla,
        ScintillaLogViewerOptions options,
        List<TokenRange> tokenRanges)
    {
        _scintilla = scintilla;
        _options = options;
        _tokenRanges = tokenRanges;
    }

    public void HandleDoubleClick()
    {
        if (!_options.EnableTokenLinks || _options.TokenLinkClicked is null)
            return;

        var position = _scintilla.CurrentPosition;
        if (TryGetTokenRangeAt(position, out var mapped))
        {
            InvokeTokenClickCallbacks(mapped.Start, mapped.Length, mapped.Payload);
            return;
        }

        var selected = _scintilla.SelectedText;
        if (string.IsNullOrWhiteSpace(selected))
            return;

        var classifier = _options.TokenClassifier;
        // ILogTokenClassifier now operates on UTF-8 bytes — encode the selected string once.
        // This is a UI event handler so a single GetBytes allocation is acceptable.
        if (classifier is null || !classifier.TryClassify(Encoding.UTF8.GetBytes(selected), out var context))
            return;

        InvokeTokenClickCallbacks(position, selected.Length, context);
    }

    public void HandleMouseUp(MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left ||
            !_options.EnableTokenLinks ||
            !_options.EnableSingleClickLinkActivation ||
            _options.TokenLinkClicked is null)
        {
            return;
        }

        if (_options.RequireCtrlForLinkActivation &&
            (System.Windows.Forms.Control.ModifierKeys & Keys.Control) != Keys.Control)
            return;

        var position = _scintilla.CharPositionFromPointClose(e.X, e.Y);
        if (position < 0 || !TryGetTokenRangeAt(position, out var tokenRange))
            return;

        InvokeTokenClickCallbacks(tokenRange.Start, tokenRange.Length, tokenRange.Payload);
    }

    public void HandleMouseMove(MouseEventArgs e)
    {
        if (!_options.EnableTokenLinks)
            return;

        var position = _scintilla.CharPositionFromPointClose(e.X, e.Y);
        var isOnLink = position >= 0 && TryGetTokenRangeAt(position, out _);
        if (isOnLink == _isHoveringLink)
            return;

        _isHoveringLink = isOnLink;
        _scintilla.Cursor = isOnLink ? Cursors.Hand : Cursors.IBeam;
    }

    public void HandleMouseLeave()
    {
        if (!_isHoveringLink)
            return;

        _isHoveringLink = false;
        _scintilla.Cursor = Cursors.IBeam;
    }

    public void TrimTokenRangesIfNeeded(int maxCount)
    {
        if (_tokenRanges.Count > maxCount)
            _tokenRanges.Clear();
    }

    private bool TryGetTokenRangeAt(int position, out TokenRange tokenRange)
    {
        var lo = 0;
        var hi = _tokenRanges.Count - 1;
        while (lo <= hi)
        {
            var mid = lo + ((hi - lo) / 2);
            var mapped = _tokenRanges[mid];
            if (position < mapped.Start)
            {
                hi = mid - 1;
                continue;
            }

            var end = mapped.Start + mapped.Length;
            if (position >= end)
            {
                lo = mid + 1;
                continue;
            }

            tokenRange = mapped;
            return true;
        }

        tokenRange = default;
        return false;
    }

    private void InvokeTokenClickCallbacks(int position, int length, ILogTokenPayload payload)
    {
        try
        {
            _options.EnrichmentCallbacks?.OnTokenClick(new TokenClickContext(position, length, payload));
        }
        catch (Exception ex)
        {
            _options.EnrichmentErrorSink?.Invoke(ex);
        }

        _options.TokenLinkClicked?.Invoke(payload);
    }
}
