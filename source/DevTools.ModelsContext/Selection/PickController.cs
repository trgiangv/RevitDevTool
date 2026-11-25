using DevTools.ModelsContext.Documents;
using System.Globalization;

namespace DevTools.ModelsContext.Selection;

public sealed class PickController(ISelectionPort selection)
{
    public const string PickingCue = "Picking · Esc to finish";
    public const string NoLinkReason = "Check a link to pick in it";
    public const string NoCategoryReason = "Check a category to pick";
    public const string CancelledCue = "Pick cancelled";
    public const string NothingCue = "Nothing picked";

    private int _picking;

    public bool IsPicking => Volatile.Read(ref _picking) == 1;

    public event EventHandler? PickingChanged;

    public static string? DisabledReason(IEnumerable<DocumentKey> documents, IEnumerable<long> categoryIds)
    {
        if (!documents.Any(document => !document.IsCurrentModel))
        {
            return NoLinkReason;
        }

        return categoryIds.Any() ? null : NoCategoryReason;
    }

    public bool CanPick(IEnumerable<DocumentKey> documents, IEnumerable<long> categoryIds)
    {
        return !IsPicking && DisabledReason(documents, categoryIds) is null;
    }

    public async Task<PickResult> PickAsync(
        IEnumerable<DocumentKey> documents,
        IEnumerable<long> categoryIds,
        CancellationToken cancellationToken)
    {
        var links = documents.Where(document => !document.IsCurrentModel).Distinct().ToList();
        var categories = categoryIds.Distinct().ToList();
        if (links.Count == 0 || categories.Count == 0 || Interlocked.CompareExchange(ref _picking, 1, 0) != 0)
        {
            return PickResult.Cancel;
        }

        PickingChanged?.Invoke(this, EventArgs.Empty);
        try
        {
            return await selection.PickAsync(links, categories, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return PickResult.Cancel;
        }
        finally
        {
            Volatile.Write(ref _picking, 0);
            PickingChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public static string StatusOf(PickResult result)
    {
        if (result.Cancelled)
        {
            return CancelledCue;
        }

        return result.Count == 0
            ? NothingCue
            : "Picked " + result.Count.ToString(CultureInfo.InvariantCulture);
    }
}
