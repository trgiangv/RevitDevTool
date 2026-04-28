using Autodesk.AutoCAD.ApplicationServices.Core;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Trace = System.Diagnostics.Trace;

namespace AcadCSharpDemo;

[PublicAPI]
public static class EditorCommands
{
    [CommandMethod("SelectEntity")]
    public static void SelectEntity()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null)
        {
            Trace.TraceWarning("SelectEntity: no active document.");
            return;
        }

        var ed = doc.Editor;
        var peo = new PromptEntityOptions("\nSelect an entity: ")
        {
            AllowNone = true
        };

        var result = ed.GetEntity(peo);

        if (!EnsurePromptOk(ed, result.Status, "SelectEntity"))
        {
            return;
        }

        using var tr = doc.TransactionManager.StartTransaction();
        var entity = tr.GetObject(result.ObjectId, OpenMode.ForRead) as Entity;
        if (entity == null)
        {
            const string message = "SelectEntity: selected object could not be opened as an entity.";
            ed.WriteMessage($"\n{message}");
            Trace.TraceWarning(message);
            // tr.Commit();
            return;
        }

        var selectedEntityMessage = $"SelectEntity: {entity.GetType().Name} selected (Layer: {entity.Layer}, Handle: {entity.Handle}).";
        ed.WriteMessage($"\n{selectedEntityMessage}");
        Trace.TraceInformation(selectedEntityMessage);

        Trace.TraceInformation($"Entity Type: {entity.GetType().Name}");
        Trace.TraceInformation($"ObjectId: {entity.ObjectId}");
        Trace.TraceInformation($"Layer: {entity.Layer}");
        Trace.TraceInformation($"Color: {entity.Color}");
        Trace.TraceInformation($"Handle: {entity.Handle}");

        var extents = entity.GeometricExtents;
        Trace.TraceInformation($"MinPoint: {extents.MinPoint}");
        Trace.TraceInformation($"MaxPoint: {extents.MaxPoint}");

        tr.Commit();
    }

    [CommandMethod("SelectMultiple")]
    public static void SelectMultiple()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null)
        {
            Trace.TraceWarning("SelectMultiple: no active document.");
            return;
        }

        var ed = doc.Editor;
        var options = new PromptSelectionOptions
        {
            MessageForAdding = "\nSelect entities: "
        };

        var result = ed.GetSelection(options);
        if (!EnsurePromptOk(ed, result.Status, "SelectMultiple"))
        {
            return;
        }

        var selectionSet = result.Value;
        var selectedCountMessage = $"SelectMultiple: selected {selectionSet.Count} entities.";
        ed.WriteMessage($"\n{selectedCountMessage}");
        Trace.TraceInformation(selectedCountMessage);

        using var tr = doc.TransactionManager.StartTransaction();
        foreach (SelectedObject selObj in selectionSet)
        {
            if (selObj == null) continue;
            var entity = tr.GetObject(selObj.ObjectId, OpenMode.ForRead) as Entity;
            if (entity == null) continue;

            var itemMessage = $"  {entity.GetType().Name} | Layer: {entity.Layer} | Handle: {entity.Handle}";
            ed.WriteMessage($"\n{itemMessage}");
            Trace.TraceInformation(itemMessage);
        }
        tr.Commit();
    }

    private static bool EnsurePromptOk(Editor ed, PromptStatus status, string operationName)
    {
        if (status == PromptStatus.OK)
        {
            return true;
        }

        string message;
        var isWarning = false;
        switch (status)
        {
            case PromptStatus.None:
                message = $"{operationName}: no input received (Enter pressed).";
                break;
            case PromptStatus.Cancel:
                message = $"{operationName}: cancelled by user (Esc/Ctrl+C).";
                break;
            case PromptStatus.Keyword:
                message = $"{operationName}: keyword selected.";
                break;
            default:
                message = $"{operationName}: prompt ended with status {status}.";
                isWarning = true;
                break;
        }

        ed.WriteMessage($"\n{message}");
        if (isWarning)
        {
            Trace.TraceWarning(message);
        }
        else
        {
            Trace.TraceInformation(message);
        }

        return false;
    }

    [CommandMethod("DocumentInfo")]
    public static void DocumentInfo()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null)
        {
            Trace.TraceWarning("No active document");
            return;
        }

        Trace.TraceInformation($"Document: {doc.Name}");
        Trace.TraceInformation($"Database Filename: {doc.Database.Filename}");
        Trace.TraceInformation($"AutoCAD Version: {Application.Version}");
        Trace.TraceInformation($"Document Count: {Application.DocumentManager.Count}");

        using var tr = doc.TransactionManager.StartTransaction();
        var bt = tr.GetObject(doc.Database.BlockTableId, OpenMode.ForRead) as BlockTable;
        if (bt == null) { tr.Commit(); return; }

        var btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead) as BlockTableRecord;
        if (btr == null) { tr.Commit(); return; }

        var count = 0;
        foreach (var _ in btr) count++;
        Trace.TraceInformation($"Entities in ModelSpace: {count}");

        tr.Commit();
    }

    [CommandMethod("ListLayers")]
    public static void ListLayers()
    {
        var doc = Application.DocumentManager.MdiActiveDocument;
        if (doc == null) return;

        using var tr = doc.TransactionManager.StartTransaction();
        var lt = tr.GetObject(doc.Database.LayerTableId, OpenMode.ForRead) as LayerTable;
        if (lt == null) { tr.Commit(); return; }

        Trace.TraceInformation("=== Layers ===");
        foreach (var layerId in lt)
        {
            var layer = tr.GetObject(layerId, OpenMode.ForRead) as LayerTableRecord;
            if (layer == null) continue;
            var status = layer.IsFrozen ? " [Frozen]" : layer.IsOff ? " [Off]" : "";
            Trace.TraceInformation($"  {layer.Name}{status} | Color: {layer.Color}");
        }
        tr.Commit();
    }
}
