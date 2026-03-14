using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using RevitMcpToolSet.Data;
using RevitMcpToolSet.Utilities;
namespace RevitMcpToolSet.Tools;

[McpServerToolType]
public static class ScheduleTools
{
    [McpServerTool(Name = "revit_create_schedule", Title = "Create Schedule", ReadOnly = false)]
    [Description("Creates a new schedule for a category and adds specified fields.")]
    public static object CreateSchedule(
        [Description("Category name for the schedule")] string categoryName,
        [Description("Field names to add to the schedule")] string[]? fieldNames = null)
    {
        if (string.IsNullOrWhiteSpace(categoryName)) throw new McpException("Category name cannot be empty.");

        var doc = Context.ActiveDocument ?? throw new McpException("No active document.");

        Category? category = null;
        foreach (Category cat in doc.Settings.Categories)
        {
            if (cat.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase))
            {
                category = cat;
                break;
            }
        }
        if (category is null) throw new McpException($"Category '{categoryName}' not found.");

        using var tx = new Transaction(doc, "Create Schedule");
        tx.Start();
        try
        {
            var schedule = ViewSchedule.CreateSchedule(doc, category.Id);
            schedule.Name = $"{categoryName} Schedule {DateTime.Now:yyyy-MM-dd_HH-mm-ss}";

            if (fieldNames is { Length: > 0 })
            {
                var schedulableFields = schedule.Definition.GetSchedulableFields();
                foreach (var fieldName in fieldNames)
                {
                    var sf = schedulableFields.FirstOrDefault(f =>
                        f.GetName(doc).Equals(fieldName, StringComparison.OrdinalIgnoreCase));
                    if (sf is not null)
                        schedule.Definition.AddField(sf);
                }
            }

            tx.Commit();
            return new { status = "Success", scheduleId = schedule.Id.Value, scheduleName = schedule.Name };
        }
        catch (Exception ex)
        {
            if (tx.HasStarted()) tx.RollBack();
            throw new McpException($"Failed to create schedule: {ex.Message}");
        }
    }

    [McpServerTool(Name = "revit_list_schedules", Title = "List Schedules", ReadOnly = true)]
    [Description("Returns all schedules in the document.")]
    public static object GetAllSchedules()
    {
        var doc = Context.ActiveDocument ?? throw new McpException("No active document.");

        var schedules = new FilteredElementCollector(doc).OfClass(typeof(ViewSchedule))
            .Cast<ViewSchedule>()
            .Select(s => new { name = s.Name, id = s.Id.Value })
            .ToList();

        return schedules.Count == 0
            ? (object)new { message = "No schedules found in the document." }
            : new { schedules };
    }

    [McpServerTool(Name = "revit_find_schedule", Title = "Find Schedule by Name", ReadOnly = true)]
    [Description("Finds a schedule by name and returns its ID.")]
    public static object GetScheduleByName(
        [Description("Schedule name to find")] string scheduleName)
    {
        if (string.IsNullOrWhiteSpace(scheduleName)) throw new McpException("Schedule name cannot be empty.");

        var doc = Context.ActiveDocument ?? throw new McpException("No active document.");

        var schedule = new FilteredElementCollector(doc).OfClass(typeof(ViewSchedule))
            .Cast<ViewSchedule>()
            .FirstOrDefault(s => s.Name.Equals(scheduleName, StringComparison.OrdinalIgnoreCase))
            ?? throw new McpException($"Schedule with name '{scheduleName}' not found.");

        return new { scheduleId = schedule.Id.Value };
    }

    [McpServerTool(Name = "revit_list_schedule_fields", Title = "List Schedulable Fields", ReadOnly = true)]
    [Description("Returns all schedulable field names for a category.")]
    public static object GetSchedulableFields(
        [Description("Category name")] string categoryName)
    {
        if (string.IsNullOrWhiteSpace(categoryName)) throw new McpException("Category name cannot be empty.");

        var doc = Context.ActiveDocument ?? throw new McpException("No active document.");

        Category? category = null;
        foreach (Category cat in doc.Settings.Categories)
        {
            if (cat.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase))
            {
                category = cat;
                break;
            }
        }
        if (category is null) throw new McpException($"Category '{categoryName}' not found.");

        using var tx = new Transaction(doc, "Temporary Schedule for Field Discovery");
        tx.Start();
        try
        {
            var tempSchedule = ViewSchedule.CreateSchedule(doc, category.Id);
            var fields = tempSchedule.Definition.GetSchedulableFields()
                .Select(f => f.GetName(doc))
                .ToArray();
            return new { fields };
        }
        finally
        {
            if (tx.HasStarted()) tx.RollBack();
        }
    }

    [McpServerTool(Name = "revit_sort_schedule", Title = "Sort Schedule", ReadOnly = false)]
    [Description("Adds sort rules to an existing schedule.")]
    public static object SortSchedule(
        [Description("Schedule element ID")] long scheduleId,
        [Description("Sort rule configurations")] ScheduleSortRule[] sortFields)
    {
        if (sortFields.Length == 0) throw new McpException("At least one sort field is required.");

        var doc = Context.ActiveDocument ?? throw new McpException("No active document.");
        var schedule = doc.GetElement(new ElementId(scheduleId)) as ViewSchedule
            ?? throw new McpException($"Schedule {scheduleId} not found.");

        using var tx = new Transaction(doc, "Add Schedule Sorting");
        tx.Start();
        try
        {
            foreach (var sortInput in sortFields.OrderBy(s => s.SortOrder))
            {
                var field = ScheduleUtils.FindScheduleField(schedule.Definition, sortInput.FieldName)
                    ?? throw new McpException($"Field '{sortInput.FieldName}' not found in schedule.");
                var sortGroupField = new ScheduleSortGroupField(field.FieldId, sortInput.Direction);
                schedule.Definition.AddSortGroupField(sortGroupField);
            }
            tx.Commit();
            return new { status = "Success" };
        }
        catch (Exception ex)
        {
            if (tx.HasStarted()) tx.RollBack();
            throw new McpException($"Failed to add schedule sorting: {ex.Message}");
        }
    }

    [McpServerTool(Name = "revit_group_schedule", Title = "Group Schedule", ReadOnly = false)]
    [Description("Adds grouping rules to an existing schedule.")]
    public static object GroupSchedule(
        [Description("Schedule element ID")] long scheduleId,
        [Description("Group rule configurations")] ScheduleGroupRule[] groupFields)
    {
        if (groupFields.Length == 0) throw new McpException("At least one group field is required.");

        var doc = Context.ActiveDocument ?? throw new McpException("No active document.");
        var schedule = doc.GetElement(new ElementId(scheduleId)) as ViewSchedule
            ?? throw new McpException($"Schedule {scheduleId} not found.");

        using var tx = new Transaction(doc, "Add Schedule Grouping");
        tx.Start();
        try
        {
            foreach (var groupInput in groupFields)
            {
                var field = ScheduleUtils.FindScheduleField(schedule.Definition, groupInput.FieldName)
                    ?? throw new McpException($"Field '{groupInput.FieldName}' not found in schedule.");
                var sortGroupField = new ScheduleSortGroupField(field.FieldId, ScheduleSortOrder.Ascending)
                {
                    ShowHeader = groupInput.ShowHeader,
                    ShowFooter = groupInput.ShowFooter,
                    ShowBlankLine = groupInput.ShowBlankLine,
                };
                schedule.Definition.AddSortGroupField(sortGroupField);
            }
            tx.Commit();
            return new { status = "Success" };
        }
        catch (Exception ex)
        {
            if (tx.HasStarted()) tx.RollBack();
            throw new McpException($"Failed to add schedule grouping: {ex.Message}");
        }
    }

    [McpServerTool(Name = "revit_filter_schedule", Title = "Filter Schedule", ReadOnly = false)]
    [Description("Adds filter conditions to an existing schedule.")]
    public static object FilterSchedule(
        [Description("Schedule element ID")] long scheduleId,
        [Description("Filter rule configurations")] ScheduleFilterRule[] groupFields)
    {
        if (groupFields.Length == 0) throw new McpException("At least one filter is required.");

        var doc = Context.ActiveDocument ?? throw new McpException("No active document.");
        var schedule = doc.GetElement(new ElementId(scheduleId)) as ViewSchedule
            ?? throw new McpException($"Schedule {scheduleId} not found.");

        var validFilterTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Equal", "NotEqual", "Contains", "NotContains", "GreaterThan", "LessThan",
            "GreaterThanOrEqual", "LessThanOrEqual", "BeginsWith", "EndsWith", "HasNoValue", "HasValue",
        };

        foreach (var f in groupFields)
        {
            if (!validFilterTypes.Contains(f.FilterType))
                throw new McpException($"Invalid filter type '{f.FilterType}'. Valid values: {string.Join(", ", validFilterTypes)}");
            if (f.IsNumeric && !double.TryParse(f.Value, out _))
                throw new McpException($"Filter value '{f.Value}' is not a valid numeric value.");
        }

        using var tx = new Transaction(doc, "Add Schedule Filters");
        tx.Start();
        try
        {
            foreach (var filterInput in groupFields)
            {
                var field = ScheduleUtils.FindScheduleField(schedule.Definition, filterInput.FieldName)
                    ?? throw new McpException($"Field '{filterInput.FieldName}' not found in schedule.");

                var filterType = filterInput.FilterType.ToLowerInvariant() switch
                {
                    "equal" => ScheduleFilterType.Equal,
                    "notequal" => ScheduleFilterType.NotEqual,
                    "contains" => ScheduleFilterType.Contains,
                    "notcontains" => ScheduleFilterType.NotContains,
                    "greaterthan" => ScheduleFilterType.GreaterThan,
                    "lessthan" => ScheduleFilterType.LessThan,
                    "greaterthanorequal" => ScheduleFilterType.GreaterThanOrEqual,
                    "lessthanorequal" => ScheduleFilterType.LessThanOrEqual,
                    "beginswith" => ScheduleFilterType.BeginsWith,
                    "endswith" => ScheduleFilterType.EndsWith,
                    "hasnovalue" => ScheduleFilterType.HasNoValue,
                    "hasvalue" => ScheduleFilterType.HasValue,
                    _ => ScheduleFilterType.Equal,
                };

                ScheduleFilter scheduleFilter;
                if (filterInput.IsNumeric)
                {
                    double.TryParse(filterInput.Value, out var numVal);
                    scheduleFilter = new ScheduleFilter(field.FieldId, filterType, numVal);
                }
                else
                {
                    scheduleFilter = new ScheduleFilter(field.FieldId, filterType, filterInput.Value);
                }

                schedule.Definition.AddFilter(scheduleFilter);
            }
            tx.Commit();
            return new { status = "Success" };
        }
        catch (Exception ex)
        {
            if (tx.HasStarted()) tx.RollBack();
            throw new McpException($"Failed to add schedule filters: {ex.Message}");
        }
    }

    [McpServerTool(Name = "revit_place_schedule_on_sheet", Title = "Place Schedule on Sheet", ReadOnly = false)]
    [Description("Places a schedule onto a drawing sheet at a specified position.")]
    public static object PlaceScheduleOnSheet(
        [Description("Sheet element ID")] long sheetId,
        [Description("Schedule element ID")] long scheduleId,
        [Description("Position on sheet [X, Y] in feet (optional)")] double[]? schedulePosition = null)
    {
        if (schedulePosition is not null && schedulePosition.Length < 2)
            throw new McpException("schedulePosition must have at least 2 values [X, Y].");

        var doc = Context.ActiveDocument ?? throw new McpException("No active document.");
        var sheet = doc.GetElement(new ElementId(sheetId)) as ViewSheet
            ?? throw new McpException($"Sheet {sheetId} not found.");
        var schedule = doc.GetElement(new ElementId(scheduleId)) as ViewSchedule
            ?? throw new McpException($"Schedule {scheduleId} not found.");

        var position = schedulePosition is { Length: >= 2 }
            ? new XYZ(schedulePosition[0], schedulePosition[1], 0)
            : XYZ.Zero;

        using var tx = new Transaction(doc, "Place Schedule On Sheet");
        tx.Start();
        try
        {
            var instance = ScheduleSheetInstance.Create(doc, sheet.Id, schedule.Id, position);
            tx.Commit();
            return new { status = "Success", instanceId = instance.Id.Value };
        }
        catch (Exception ex)
        {
            if (tx.HasStarted()) tx.RollBack();
            throw new McpException($"Failed to place schedule on sheet: {ex.Message}");
        }
    }
}
