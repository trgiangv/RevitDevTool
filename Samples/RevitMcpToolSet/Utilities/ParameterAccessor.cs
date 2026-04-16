namespace RevitMcpToolSet.Utilities;

internal static class ParameterAccessor
{
    internal static string GetParameterValue(Parameter parameter)
    {
        try
        {
            return parameter.StorageType switch
            {
                StorageType.Integer => parameter.AsInteger().ToString(),
                StorageType.Double => parameter.AsDouble().ToString("G15"),
                StorageType.String => parameter.AsString() ?? "",
                StorageType.ElementId => parameter.AsElementId()?.ToString() ?? "",
                _ => parameter.AsValueString() ?? "",
            };
        }
        catch { return ""; }
    }

    internal static (bool success, string message, ElementId newElemId) ChangeType(Element element, long newTypeId)
    {
        try
        {
            var newId = element.ChangeTypeId(newTypeId.ToElementId());
            return (true, $"Change type to '{newTypeId}'", newId);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return (false, $"Unexpected error changing type: {ex.Message}", ElementId.InvalidElementId);
        }
    }

    internal static (bool success, string message) SetParameterValue(Element element, string parameterName, string value)
    {
        try
        {
            var parameters = element.GetParameters(parameterName);
            if (parameters.Count == 0)
                return (false, $"Parameter '{parameterName}' not found");

            var writable = parameters.FirstOrDefault(p => !p.IsReadOnly);
            if (writable is null)
                return (false, $"Parameter '{parameterName}' is read-only");

            bool success;
            try
            {
                success = writable.StorageType switch
                {
                    StorageType.Integer when int.TryParse(value, out var intVal) => writable.Set(intVal),
                    StorageType.Integer => throw new FormatException($"Invalid integer value '{value}' for parameter '{parameterName}'"),
                    StorageType.Double when double.TryParse(value, out var dblVal) => writable.Set(dblVal),
                    StorageType.Double => throw new FormatException($"Invalid numeric value '{value}' for parameter '{parameterName}'"),
                    StorageType.String => writable.Set(value),
                    StorageType.ElementId when long.TryParse(value, out var eid) => writable.Set(eid.ToElementId()),
                    StorageType.ElementId => throw new FormatException($"Invalid element ID '{value}' for parameter '{parameterName}'"),
                    _ => throw new NotSupportedException($"Unsupported parameter storage type '{writable.StorageType}' for parameter '{parameterName}'"),
                };
            }
            catch (FormatException ex) { return (false, ex.Message); }
            catch (NotSupportedException ex) { return (false, ex.Message); }
            catch (InvalidOperationException ex) { return (false, $"Failed to set parameter '{parameterName}': {ex.Message}"); }

            return success
                ? (true, $"Set parameter '{parameterName}' to '{value}'")
                : (false, $"Parameter '{parameterName}' rejected the value '{value}'. The parameter may have constraints or the value may be out of range.");
        }
        catch (Exception ex)
        {
            return (false, $"Unexpected error setting parameter '{parameterName}': {ex.Message}");
        }
    }

    internal static string? GetBuiltInParam(Parameter parameter)
    {
        return (parameter.Definition as InternalDefinition)?.BuiltInParameter.ToString();
    }
}
