namespace DevTools.Update;

/// <summary>
///     A feed, signature, or payload is not correct. The engine applies nothing from it.
/// </summary>
public sealed class UpdateIntegrityException(string message) : Exception(message);
