using DevTools.Execution.Interfaces;
using RevitDevTool.Core;
using RevitDevTool.Utils;

namespace RevitDevTool.HostAdapters;

public sealed class RevitDocumentBridge(IHostContextExecutor executor) : IDocumentBridge
{
    public Task<DocumentOperationResult> OpenDocumentAsync(string filePath, CancellationToken ct) =>
        executor.ExecuteAsync(() =>
        {
            try
            {
                var uiDoc = RevitContext.UiApplication.OpenAndActivateDocument(filePath);
                return new DocumentOperationResult(true, $"Opened '{uiDoc.Document.Title}'.", uiDoc.Document.Title);
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
                var uiApp = RevitContext.UiApplication;
                if (!uiApp.HasActiveUiDocument())
                    return new DocumentOperationResult(false, "No active document to close.");

                var title = RevitContext.ActiveDocument?.Title ?? "Unknown";
                uiApp.CloseActiveUiDocument(save);
                return new DocumentOperationResult(true, $"Closed '{title}'.", title);
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
                var doc = RevitContext.ActiveDocument;
                if (doc is null)
                    return new DocumentOperationResult(false, "No active document to save.");

                if (string.IsNullOrWhiteSpace(savePath))
                    doc.Save();
                else
                    doc.SaveAs(savePath!);

                return new DocumentOperationResult(true, $"Saved '{doc.Title}'.", doc.Title);
            }
            catch (Exception ex)
            {
                return new DocumentOperationResult(false, $"Failed to save document: {ex.Message}");
            }
        }, ct);
}
