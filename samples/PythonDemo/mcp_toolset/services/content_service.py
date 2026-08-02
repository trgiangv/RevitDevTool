"""Multimodal MCP content demonstrations."""
from __future__ import annotations

import csv
import io
import os
import tempfile

from Autodesk.Revit import DB

from shared.element_helpers import element_id_value, require_active_view, require_doc
from shared.responses import ToolError


class ContentService:
    def capture_view(self, resolution: int = 150) -> tuple[bytes, str, int, str]:
        doc = require_doc()
        view = require_active_view()
        temp_dir = tempfile.mkdtemp(prefix="revit_mcp_capture_")
        export_base = os.path.join(temp_dir, "capture")
        options = DB.ImageExportOptions()
        options.ExportRange = DB.ExportRange.SetOfViews
        options.FilePath = export_base
        options.HLRandWFViewsFileType = DB.ImageFileType.PNG
        options.ShadowViewsFileType = DB.ImageFileType.PNG
        options.ZoomType = DB.ZoomFitType.FitToPage
        options.PixelSize = 1280
        options.ImageResolution = _map_dpi(resolution)
        options.SetViewsAndSheets([view.Id])
        doc.ExportImage(options)

        prefix = os.path.basename(export_base)
        image_path = next(
            (
                os.path.join(temp_dir, name)
                for name in sorted(os.listdir(temp_dir))
                if name.startswith(prefix)
            ),
            None,
        )
        if image_path is None:
            raise ToolError("View capture completed but no image file was produced")

        with open(image_path, "rb") as stream:
            data = stream.read()
        return data, view.Name, element_id_value(view.Id), image_path

    def preview_schedule(self, schedule_id: int, max_rows: int = 30) -> tuple[str, str, int, int, int]:
        doc = require_doc()
        schedule = doc.GetElement(DB.ElementId(schedule_id))
        if not isinstance(schedule, DB.ViewSchedule):
            raise ToolError("Schedule {} not found".format(schedule_id))

        columns, all_rows = _read_schedule_table(schedule)
        limit = max_rows if max_rows > 0 else 30
        preview_rows = all_rows[:limit]
        csv_text = _build_csv(columns, preview_rows)
        return schedule.Name, csv_text, len(preview_rows), len(all_rows), len(columns)

    def model_digest(self) -> dict:
        doc = require_doc()
        views = (
            DB.FilteredElementCollector(doc)
            .OfClass(DB.View)
            .WhereElementIsNotElementType()
            .ToElements()
        )
        view_count = sum(
            1
            for view in views
            if not view.IsTemplate
            and view.ViewType not in (DB.ViewType.Undefined, DB.ViewType.Internal)
            and not isinstance(view, DB.ViewSchedule)
        )
        level_count = DB.FilteredElementCollector(doc).OfClass(DB.Level).GetElementCount()
        warnings = doc.GetWarnings()
        warning_count = len(warnings) if warnings else 0
        return {
            "projectTitle": doc.Title,
            "viewCount": view_count,
            "levelCount": level_count,
            "warningCount": warning_count,
        }


def _map_dpi(resolution: int) -> DB.ImageResolution:
    if resolution <= 72:
        return DB.ImageResolution.DPI_72
    if resolution <= 150:
        return DB.ImageResolution.DPI_150
    if resolution <= 300:
        return DB.ImageResolution.DPI_300
    return DB.ImageResolution.DPI_600


def _read_schedule_table(schedule: DB.ViewSchedule) -> tuple[list[str], list[dict[str, str]]]:
    table_data = schedule.GetTableData()
    body = table_data.GetSectionData(DB.SectionType.Body)
    header = table_data.GetSectionData(DB.SectionType.Header)
    column_count = body.NumberOfColumns
    if column_count <= 0:
        raise ToolError("Schedule has no columns to preview")

    if header.NumberOfRows > 0:
        columns = [
            schedule.GetCellText(DB.SectionType.Header, 0, col)
            for col in range(column_count)
        ]
    else:
        columns = ["Column_{}".format(i + 1) for i in range(column_count)]

    rows: list[dict[str, str]] = []
    for row_index in range(body.NumberOfRows):
        row: dict[str, str] = {}
        for col in range(column_count):
            row[columns[col]] = schedule.GetCellText(DB.SectionType.Body, row_index, col)
        rows.append(row)
    return columns, rows


def _build_csv(columns: list[str], rows: list[dict[str, str]]) -> str:
    buffer = io.StringIO()
    writer = csv.writer(buffer)
    writer.writerow(columns)
    for row in rows:
        writer.writerow([row.get(column, "") for column in columns])
    return buffer.getvalue()
