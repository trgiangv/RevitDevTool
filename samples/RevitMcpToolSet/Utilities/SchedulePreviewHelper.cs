using System.Text;
using Autodesk.Revit.DB;

namespace RevitMcpToolSet.Utilities;

internal static class SchedulePreviewHelper
{
    public const int DefaultPreviewRows = 30;

    public static (string Csv, int EmbeddedRows, int TotalRows, int ColumnCount) BuildPreviewCsv(
        ViewSchedule schedule,
        int? maxRows = null)
    {
        var limit = maxRows is > 0 ? maxRows.Value : DefaultPreviewRows;
        var (columns, allRows) = ReadScheduleTable(schedule);
        var previewRows = allRows.Take(limit).ToList();
        return (BuildCsv(columns, previewRows), previewRows.Count, allRows.Count, columns.Count);
    }

    public static (List<string> Columns, List<Dictionary<string, string>> Rows) ReadScheduleTable(ViewSchedule schedule)
    {
        var tableData = schedule.GetTableData();
        var bodySection = tableData.GetSectionData(SectionType.Body);
        var headerSection = tableData.GetSectionData(SectionType.Header);
        var columnCount = bodySection.NumberOfColumns;
        if (columnCount <= 0)
            throw new InvalidOperationException("Schedule has no columns to preview.");

        var headerColumnCount = headerSection.NumberOfColumns;
        var columns = new List<string>();
        for (var col = 0; col < columnCount; col++)
        {
            if (headerSection.NumberOfRows > 0 && col < headerColumnCount)
                columns.Add(schedule.GetCellText(SectionType.Header, 0, col));
            else
                columns.Add($"Column_{col + 1}");
        }

        var rows = new List<Dictionary<string, string>>();
        for (var rowIndex = 0; rowIndex < bodySection.NumberOfRows; rowIndex++)
        {
            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var col = 0; col < columnCount; col++)
                row[columns[col]] = schedule.GetCellText(SectionType.Body, rowIndex, col);
            rows.Add(row);
        }

        return (columns, rows);
    }

    public static string BuildCsv(IReadOnlyList<string> columns, IReadOnlyList<Dictionary<string, string>> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(",", columns.Select(EscapeCsv)));
        foreach (var row in rows)
            builder.AppendLine(string.Join(",", columns.Select(column =>
                EscapeCsv(row.TryGetValue(column, out var value) ? value : ""))));
        return builder.ToString();
    }

    private static string EscapeCsv(string? value)
    {
        value ??= "";
        if (value.Contains('"') || value.Contains(',') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}
