using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Nice3point.Revit.Toolkit;
using RevitMcpToolSet.Data;
using RevitMcpToolSet.Utilities;

namespace RevitMcpToolSet.Tools;

[McpServerToolType]
[Description("Tools for creating and configuring schedules in Revit.")]
public static class ScheduleTools
{
    [McpServerTool(Name = "revit_create_schedule", Title = "Create Schedule", ReadOnly = false)]
    [Description("Creates a new schedule and applies fields, sorting, grouping, and filters.")]
    public static object CreateSchedule(
        [Description("Schedule configuration")] ScheduleConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.CategoryName))
            throw new McpException("config.categoryName is required.");

        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        var category = FindCategory(doc, config.CategoryName)
            ?? throw new McpException($"Category '{config.CategoryName}' not found.");

        using var tx = new Transaction(doc, "MCP: revit_create_schedule");
        tx.Start();
        try
        {
            var schedule = ViewSchedule.CreateSchedule(doc, category.Id);
            schedule.Name = string.IsNullOrWhiteSpace(config.ScheduleName)
                ? $"{config.CategoryName} Schedule {DateTime.Now:yyyy-MM-dd_HH-mm-ss}"
                : config.ScheduleName;

            var schedulableFields = schedule.Definition.GetSchedulableFields().ToList();
            foreach (var fieldName in config.Fields)
            {
                if (string.IsNullOrWhiteSpace(fieldName))
                    continue;

                var schedulableField = schedulableFields.FirstOrDefault(f =>
                    f.GetName(doc).Equals(fieldName, StringComparison.OrdinalIgnoreCase));
                if (schedulableField is not null)
                    schedule.Definition.AddField(schedulableField);
            }

            foreach (var sortRule in config.SortRules)
            {
                if (string.IsNullOrWhiteSpace(sortRule.Field))
                    continue;

                var field = ScheduleUtils.FindScheduleField(schedule.Definition, sortRule.Field)
                    ?? throw new McpException($"Sort field '{sortRule.Field}' not found in schedule.");
                schedule.Definition.AddSortGroupField(
                    new ScheduleSortGroupField(field.FieldId, sortRule.Direction));
            }

            foreach (var groupRule in config.GroupRules)
            {
                if (string.IsNullOrWhiteSpace(groupRule.Field))
                    continue;

                var field = ScheduleUtils.FindScheduleField(schedule.Definition, groupRule.Field)
                    ?? throw new McpException($"Group field '{groupRule.Field}' not found in schedule.");
                var sortGroupField = new ScheduleSortGroupField(field.FieldId, ScheduleSortOrder.Ascending)
                {
                    ShowHeader = groupRule.ShowHeader,
                    ShowFooter = groupRule.ShowFooter,
                };
                schedule.Definition.AddSortGroupField(sortGroupField);
            }

            foreach (var filterRule in config.FilterRules)
            {
                if (string.IsNullOrWhiteSpace(filterRule.Field))
                    continue;

                var field = ScheduleUtils.FindScheduleField(schedule.Definition, filterRule.Field)
                    ?? throw new McpException($"Filter field '{filterRule.Field}' not found in schedule.");

                var filterType = MapFilterType(filterRule.Operator);
                ScheduleFilter scheduleFilter;
                if (filterRule.IsNumeric)
                {
                    if (!double.TryParse(filterRule.Value, out var numericValue))
                        throw new McpException($"Filter value '{filterRule.Value}' is not a valid numeric value.");
                    scheduleFilter = new ScheduleFilter(field.FieldId, filterType, numericValue);
                }
                else
                {
                    scheduleFilter = new ScheduleFilter(field.FieldId, filterType, filterRule.Value);
                }

                schedule.Definition.AddFilter(scheduleFilter);
            }

            tx.Commit();
            return new
            {
                scheduleId = schedule.Id.ToValue(),
                fieldCount = schedule.Definition.GetFieldCount(),
            };
        }
        catch (McpException)
        {
            if (tx.HasStarted()) tx.RollBack();
            throw;
        }
        catch (Exception ex)
        {
            if (tx.HasStarted()) tx.RollBack();
            throw new McpException($"Failed to create schedule: {ex.Message}");
        }
    }

    [McpServerTool(Name = "revit_list_schedule_fields", Title = "List Schedulable Fields", ReadOnly = true)]
    [Description("Returns schedulable field names and types for a category.")]
    public static object ListScheduleFields(
        [Description("Category name")] string categoryName)
    {
        if (string.IsNullOrWhiteSpace(categoryName))
            throw new McpException("Category name cannot be empty.");

        var doc = RevitContext.ActiveDocument ?? throw new McpException("No active document.");
        var category = FindCategory(doc, categoryName)
            ?? throw new McpException($"Category '{categoryName}' not found.");

        using var tx = new Transaction(doc, "MCP: revit_list_schedule_fields");
        tx.Start();
        try
        {
            var tempSchedule = ViewSchedule.CreateSchedule(doc, category.Id);
            var fields = tempSchedule.Definition.GetSchedulableFields()
                .Select(f => new
                {
                    name = f.GetName(doc),
                    type = f.FieldType.ToString(),
                })
                .OrderBy(f => f.name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new { fields };
        }
        finally
        {
            if (tx.HasStarted()) tx.RollBack();
        }
    }

    private static Category? FindCategory(Document doc, string categoryName)
    {
        foreach (Category cat in doc.Settings.Categories)
        {
            if (cat.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase))
                return cat;
        }

        return null;
    }

    private static ScheduleFilterType MapFilterType(string? operatorName)
    {
        if (string.IsNullOrWhiteSpace(operatorName))
            throw new McpException("Filter operator is required.");

        return operatorName.Trim().ToLowerInvariant().Replace(" ", "_") switch
        {
            "equal" or "equals" => ScheduleFilterType.Equal,
            "not_equal" or "not_equals" or "notequal" => ScheduleFilterType.NotEqual,
            "contains" => ScheduleFilterType.Contains,
            "not_contains" or "notcontains" => ScheduleFilterType.NotContains,
            "greater_than" or "greaterthan" => ScheduleFilterType.GreaterThan,
            "less_than" or "lessthan" => ScheduleFilterType.LessThan,
            "greater_or_equal" or "greater_than_or_equal" or "greaterthanorequal" => ScheduleFilterType.GreaterThanOrEqual,
            "less_or_equal" or "less_than_or_equal" or "lessthanorequal" => ScheduleFilterType.LessThanOrEqual,
            "begins_with" or "beginswith" => ScheduleFilterType.BeginsWith,
            "ends_with" or "endswith" => ScheduleFilterType.EndsWith,
            "has_no_value" or "hasnovalue" => ScheduleFilterType.HasNoValue,
            "has_value" or "hasvalue" => ScheduleFilterType.HasValue,
            _ => throw new McpException(
                $"Invalid filter operator '{operatorName}'. " +
                "Valid values: equals, not_equals, contains, not_contains, greater_than, less_than, " +
                "greater_or_equal, less_or_equal, begins_with, ends_with, has_no_value, has_value."),
        };
    }
}
