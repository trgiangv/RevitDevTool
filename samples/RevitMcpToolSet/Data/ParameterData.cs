using System.ComponentModel;
using System.Text.Json.Serialization;

namespace RevitMcpToolSet.Data;

/// <summary>
/// Single parameter write for batch updates in <c>revit_write_parameters</c>.
/// </summary>
public class ParameterUpdate
{
    [Description("Parameter name as shown in Revit Properties.")]
    [JsonPropertyName("param_name")]
    public string ParamName { get; set; } = "";

    [Description("New parameter value (string representation).")]
    [JsonPropertyName("value")]
    public string Value { get; set; } = "";

    [JsonIgnore]
    public string ParameterName
    {
        get => ParamName;
        set => ParamName = value;
    }
}
