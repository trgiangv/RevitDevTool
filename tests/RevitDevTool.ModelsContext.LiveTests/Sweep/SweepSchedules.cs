using Nice3point.Revit.Extensions;

namespace RevitDevTool.ModelsContext.LiveTests;

internal static class SweepSchedules
{
    public static List<ScheduleRow> Count(Document document, Dictionary<long, int> oracle, Dictionary<long, int> panel, int limit, out int skipped)
    {
        var schedulable = oracle.Where(pair => pair.Value > 0)
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key)
            .Select(pair => pair.Key)
            .Where(IsSchedulable)
            .ToList();
        var chosen = schedulable.Take(limit).ToList();
        skipped = schedulable.Count - chosen.Count;
        var rows = new List<ScheduleRow>();
        if (chosen.Count == 0)
        {
            return rows;
        }

        using var transaction = new Transaction(document, "Model Context sweep schedules");
        transaction.Start();
        try
        {
            foreach (var category in chosen)
            {
                var row = new ScheduleRow
                {
                    Category = ActiveModel.CategoryName(document, category),
                    Oracle = oracle[category],
                    Panel = panel.TryGetValue(category, out var count) ? count : 0
                };
                try
                {
                    row.Schedule = Rows(document, category);
                    row.Reason = row.Schedule == row.Oracle ? SweepNative.Same : Guess(document, category);
                }
                catch (Exception exception)
                {
                    row.Reason = "schedule failed: " + exception.Message;
                }

                rows.Add(row);
            }
        }
        finally
        {
            if (transaction.HasStarted())
            {
                transaction.RollBack();
            }
        }

        return rows;
    }

    private static bool IsSchedulable(long category)
    {
        try
        {
            return ViewSchedule.IsValidCategoryForSchedule(category.ToElementId());
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static int Rows(Document document, long category)
    {
        var schedule = ViewSchedule.CreateSchedule(document, category.ToElementId());
        var definition = schedule.Definition;
        definition.IsItemized = true;
        definition.ShowHeaders = false;
        definition.ShowTitle = false;
        definition.ShowGrandTotal = false;
        var fields = definition.GetSchedulableFields();
        var field = fields.FirstOrDefault(candidate => candidate.FieldType == ScheduleFieldType.Count) ?? fields.FirstOrDefault()
            ?? throw new InvalidOperationException("the schedule has no field to show");
        definition.AddField(field);
        document.Regenerate();
        return schedule.GetTableData().GetSectionData(SectionType.Body).NumberOfRows;
    }

    private static string Guess(Document document, long category)
    {
        var elements = Oracle.Candidates(document, [category], true).ToList();
        var reasons = new List<string>();
        var secondary = elements.Count(element => !Oracle.InMainOrPrimary(element));
        if (secondary > 0)
        {
            reasons.Add($"design options ({secondary} in secondary options)");
        }

        var nested = elements.Count(element => element is FamilyInstance { SuperComponent: not null });
        if (nested > 0)
        {
            reasons.Add($"nested shared components ({nested})");
        }

        var grouped = elements.Count(element => element.GroupId != ElementId.InvalidElementId);
        if (grouped > 0)
        {
            reasons.Add($"groups ({grouped} in groups)");
        }

        reasons.Add("phase filter of the new schedule");
        return string.Join(", ", reasons);
    }
}
