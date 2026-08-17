using System.Collections;
using Autodesk.AutoCAD.Internal;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace DevTools.Mcp.Acad.Tools;

/// <summary>
/// AutoCAD history navigator using Internal.Utils APIs.
/// Resolves MdiActiveDocument per-call (no cached state).
/// DisableUndoRecording prevents undo/redo commands from polluting the stack.
/// </summary>
public sealed class AcadHistoryNavigator
{
    public bool CanGoBack => IsQuiescent && Utils.IsUndoAvailable();

    public bool CanGoForward
    {
        get
        {
            if (!IsQuiescent) return false;
            return GetForwardStack().Count > 0;
        }
    }

    public IReadOnlyList<string> GetBackStack()
    {
        try { return ToStringList(Utils.GetUndoHistory()); }
        catch { return []; }
    }

    public IReadOnlyList<string> GetForwardStack()
    {
        try { return ToStringList(Utils.GetRedoHistory()); }
        catch { return []; }
    }

    public bool GoBack(int steps = 1)
    {
        if (!CanGoBack) return false;
        var doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc == null) return false;

        var db = doc.Database;
        Utils.DisableUndoRecording(db, true);
        try
        {
            if (steps == 1)
                Utils.SendMenuStringToExecute(doc, "_.U ", false);
            else
                Utils.SendMenuStringToExecute(doc, $"_.UNDO {steps} ", false);
        }
        finally
        {
            Utils.DisableUndoRecording(db, false);
        }
        return true;
    }

    public bool GoForward(int steps = 1)
    {
        if (!CanGoForward) return false;
        var doc = AcApp.DocumentManager.MdiActiveDocument;
        if (doc == null) return false;

        var db = doc.Database;
        Utils.DisableUndoRecording(db, true);
        try
        {
            for (int i = 0; i < steps; i++)
                Utils.SendMenuStringToExecute(doc, "_.REDO ", false);
        }
        finally
        {
            Utils.DisableUndoRecording(db, false);
        }
        return true;
    }

    private static bool IsQuiescent
    {
        get
        {
            try { return Utils.IsInQuiescentState(); }
            catch { return false; }
        }
    }

    private static IReadOnlyList<string> ToStringList(object? result)
    {
        if (result is IEnumerable enumerable)
        {
            var list = new List<string>();
            foreach (var item in enumerable)
                list.Add(item?.ToString() ?? "(null)");
            return list;
        }
        return [];
    }
}
