using System.ComponentModel;
using System.Text.Json.Serialization;

namespace RevitMcpToolSet.Data;

/// <summary>
/// Standard error codes returned in write-tool <c>failures[]</c> arrays.
/// </summary>
public static class ToolErrorCodes
{
    public const string ConstraintViolation = "constraint_violation";
    public const string ElementBorrowed = "element_borrowed";
    public const string ElementPinned = "element_pinned";
    public const string GroupMember = "group_member";
    public const string ParamReadonly = "param_readonly";
    public const string TypeMismatch = "type_mismatch";
    public const string NotFound = "not_found";
}

/// <summary>
/// Standard per-element error envelope for partial-success write operations.
/// </summary>
public class ToolError
{
    [Description("Affected element ID (int64). Omitted when the error is not element-specific.")]
    [JsonPropertyName("elementId")]
    public long? ElementId { get; set; }

    [Description(
        "Machine-readable error code: constraint_violation, element_borrowed, element_pinned, " +
        "group_member, param_readonly, type_mismatch, or not_found.")]
    [JsonPropertyName("code")]
    public string Code { get; set; } = "";

    [Description("Human-readable error description.")]
    [JsonPropertyName("message")]
    public string Message { get; set; } = "";

    [Description("Whether the agent may retry after correcting scope or permissions.")]
    [JsonPropertyName("recoverable")]
    public bool Recoverable { get; set; }

    [Description("Suggested recovery action, e.g. 'release workset', 'unpin element', or 'use undo_changes'.")]
    [JsonPropertyName("suggestedAction")]
    public string? SuggestedAction { get; set; }
}
