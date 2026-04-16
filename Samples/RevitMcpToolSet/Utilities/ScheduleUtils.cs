namespace RevitMcpToolSet.Utilities;

internal static class ScheduleUtils
{
    public static ScheduleField? FindScheduleField(ScheduleDefinition definition, string fieldName)
    {
        for (var i = 0; i < definition.GetFieldCount(); i++)
        {
            var field = definition.GetField(i);
            if (field.GetName().Equals(fieldName, StringComparison.OrdinalIgnoreCase))
                return field;
        }
        return null;
    }
}
