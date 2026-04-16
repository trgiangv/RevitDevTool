namespace RevitMcpToolSet.Data;

public enum SpatialFilterMode
{
    ElementsInside,
    ElementsIntersecting,
}

public class ElementSearchCriteria
{
    public bool BasicInfo { get; set; }
    public string? SearchScope { get; set; }
    public long ViewId { get; set; } = -1;
    public bool SelectedOnly { get; set; }
    public bool IncludeTypes { get; set; }
    public bool IncludeInstances { get; set; } = true;
    public string[]? Categories { get; set; }
    public string[]? FamilyNameFilters { get; set; }
    public string[]? ElementNameFilters { get; set; }
    public string[]? LevelNameFilters { get; set; }
    public Bounds3D? BoundingBox { get; set; }
    public SpatialFilterMode? BoundingBoxFilteringMode { get; set; }
    public ParameterCondition[]? ParameterFilters { get; set; }
    public int MaxResults { get; set; } = 500;
}

public class ModelAnalysisReport
{
    public int TotalElements { get; set; }
    public Dictionary<string, int> CategoryBreakdown { get; set; } = new();
    public Dictionary<string, int> LevelDistribution { get; set; } = new();
    public Dictionary<string, int> FamilyBreakdown { get; set; } = new();
    public bool HasErrors { get; set; }
}
