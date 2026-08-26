using Autodesk.AutoCAD.ApplicationServices;
using AcadApp = Autodesk.AutoCAD.ApplicationServices.Core.Application;

namespace AcadDevTool.Adapters;

public sealed class AcadDocumentBridge(IHostContextExecutor executor) : IDocumentBridge
{
    public Task<DocumentOperationResult> OpenDocumentAsync(string filePath, CancellationToken ct) =>
        executor.ExecuteAsync(() =>
        {
            try
            {
                var doc = DocumentCollectionExtension.Open(AcadApp.DocumentManager, filePath, false);
                if (doc is null)
                    return new DocumentOperationResult(false, $"Failed to open '{filePath}'.");

                AcadApp.DocumentManager.MdiActiveDocument = doc;
                return new DocumentOperationResult(true, $"Opened '{doc.Name}'.", doc.Name);
            }
            catch (Exception ex)
            {
                return new DocumentOperationResult(false, $"Failed to open document: {ex.Message}");
            }
        }, ct);

    public Task<DocumentOperationResult> CloseDocumentAsync(bool save, CancellationToken ct) =>
        executor.ExecuteAsync(() =>
        {
            try
            {
                var doc = AcadApp.DocumentManager.MdiActiveDocument;
                if (doc is null)
                    return new DocumentOperationResult(false, "No active document to close.");

                var name = doc.Name;
                if (save)
                    doc.Database.Save();

                doc.Database.Dispose();
                return new DocumentOperationResult(true, $"Closed '{name}'.", name);
            }
            catch (Exception ex)
            {
                return new DocumentOperationResult(false, $"Failed to close document: {ex.Message}");
            }
        }, ct);

    public Task<DocumentOperationResult> SaveDocumentAsync(string? savePath, CancellationToken ct) =>
        executor.ExecuteAsync(() =>
        {
            try
            {
                var doc = AcadApp.DocumentManager.MdiActiveDocument;
                if (doc is null)
                    return new DocumentOperationResult(false, "No active document to save.");

                if (string.IsNullOrWhiteSpace(savePath))
                    doc.Database.Save();
                else
                    doc.Database.SaveAs(savePath!, Autodesk.AutoCAD.DatabaseServices.DwgVersion.Current);

                return new DocumentOperationResult(true, $"Saved '{doc.Name}'.", doc.Name);
            }
            catch (Exception ex)
            {
                return new DocumentOperationResult(false, $"Failed to save document: {ex.Message}");
            }
        }, ct);
}
