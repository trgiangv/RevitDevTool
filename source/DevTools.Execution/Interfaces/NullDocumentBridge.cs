namespace DevTools.Execution.Interfaces;

/// <summary>Fallback when the host has not registered an <see cref="IDocumentBridge"/>.</summary>
internal sealed class NullDocumentBridge : IDocumentBridge
{
    public static readonly NullDocumentBridge Instance = new();

    public Task<DocumentOperationResult> OpenDocumentAsync(string filePath, CancellationToken ct) =>
        Task.FromResult(new DocumentOperationResult(false, "open_document is not available. The host has not registered a document bridge."));

    public Task<DocumentOperationResult> CloseDocumentAsync(bool save, CancellationToken ct) =>
        Task.FromResult(new DocumentOperationResult(false, "close_document is not available. The host has not registered a document bridge."));

    public Task<DocumentOperationResult> SaveDocumentAsync(string? savePath, CancellationToken ct) =>
        Task.FromResult(new DocumentOperationResult(false, "save_document is not available. The host has not registered a document bridge."));
}
