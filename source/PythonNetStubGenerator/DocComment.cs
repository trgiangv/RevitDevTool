namespace PythonNetStubGenerator;

/// <summary>
/// Represents extracted XML documentation for a single .NET member.
/// </summary>
public sealed class DocComment
{
    /// <summary>The &lt;summary&gt; text.</summary>
    public string? Summary { get; init; }

    /// <summary>The &lt;returns&gt; text.</summary>
    public string? Returns { get; init; }

    /// <summary>The &lt;remarks&gt; text.</summary>
    public string? Remarks { get; init; }

    /// <summary>The &lt;example&gt; text.</summary>
    public string? Example { get; init; }

    /// <summary>The &lt;value&gt; text (for properties).</summary>
    public string? Value { get; init; }

    /// <summary>Parameter name → description mappings from &lt;param&gt; elements.</summary>
    public Dictionary<string, string> Parameters { get; init; } = new();

    /// <summary>Exception documentation from &lt;exception&gt; elements.</summary>
    public List<ExceptionDoc> Exceptions { get; init; } = [];

    /// <summary>Whether this doc comment has any meaningful content.</summary>
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(Summary)
        && string.IsNullOrWhiteSpace(Returns)
        && string.IsNullOrWhiteSpace(Remarks)
        && Parameters.Count == 0
        && Exceptions.Count == 0;
}

/// <summary>
/// Represents documentation for a single exception a member can throw.
/// </summary>
public sealed class ExceptionDoc
{
    /// <summary>The exception type name (e.g. "ArgumentNullException").</summary>
    public required string TypeName { get; init; }

    /// <summary>Description of when this exception is thrown.</summary>
    public required string Description { get; init; }
}
