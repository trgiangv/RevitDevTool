using RevitMcpToolSet.Data;

namespace RevitMcpToolSet.Utilities;

internal static class ToolErrorHelper
{
    internal static ToolError FromException(Exception ex, long? elementId = null)
        => FromMessage(ex.Message, elementId);

    internal static ToolError FromMessage(string message, long? elementId = null)
    {
        var lower = message.ToLowerInvariant();
        var code = ToolErrorCodes.ConstraintViolation;
        var recoverable = true;
        string? suggestedAction = null;

        if (lower.Contains("not found") || lower.Contains("does not exist"))
            code = ToolErrorCodes.NotFound;
        else if (lower.Contains("borrowed"))
        {
            code = ToolErrorCodes.ElementBorrowed;
            suggestedAction = "release workset";
        }
        else if (lower.Contains("pinned"))
        {
            code = ToolErrorCodes.ElementPinned;
            suggestedAction = "unpin element";
        }
        else if (lower.Contains("group"))
        {
            code = ToolErrorCodes.GroupMember;
            suggestedAction = "ungroup or edit group";
        }
        else if (lower.Contains("read-only") || lower.Contains("readonly"))
            code = ToolErrorCodes.ParamReadonly;
        else if (lower.Contains("type") && (lower.Contains("mismatch") || lower.Contains("change")))
            code = ToolErrorCodes.TypeMismatch;

        return new ToolError
        {
            ElementId = elementId,
            Code = code,
            Message = message,
            Recoverable = recoverable,
            SuggestedAction = suggestedAction,
        };
    }
}
