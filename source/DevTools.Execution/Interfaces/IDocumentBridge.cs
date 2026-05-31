namespace DevTools.Execution.Interfaces;

/// <summary>
/// Provides host-agnostic document operations (open, close, save).
/// Each host registers its own implementation using host-specific APIs.
/// </summary>
public interface IDocumentBridge
{
    Task<DocumentOperationResult> OpenDocumentAsync(string filePath, CancellationToken ct = default);
    Task<DocumentOperationResult> CloseDocumentAsync(bool save, CancellationToken ct = default);
    Task<DocumentOperationResult> SaveDocumentAsync(string? savePath, CancellationToken ct = default);
}

public sealed record DocumentOperationResult(bool Success, string Message, string? DocumentTitle = null);
