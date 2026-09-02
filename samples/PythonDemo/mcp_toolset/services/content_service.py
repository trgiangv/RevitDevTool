"""Multimodal MCP content demonstrations."""

import csv
import os
import tempfile

from Autodesk.Revit import DB

from dto.content import ModelDigestResult, SchedulePreviewResult, ViewCaptureResult
from shared.document_stats import count_levels, count_user_views, count_warnings
from shared.element_helpers import require_active_view, require_doc
from shared.fs_helpers import list_directory_names
from shared.image_export import map_dpi
from shared.responses import ToolError


class ContentService:
    @staticmethod
    def capture_view(resolution: int = 150) -> ViewCaptureResult:
        doc = require_doc()
        view = require_active_view(doc)
        temp_dir = tempfile.mkdtemp(prefix="revit_mcp_capture_")
        export_base = os.path.join(temp_dir, "capture")
        options = DB.ImageExportOptions()
        options.ExportRange = DB.ExportRange.SetOfViews
        options.FilePath = export_base
        options.HLRandWFViewsFileType = DB.ImageFileType.PNG
        options.ShadowViewsFileType = DB.ImageFileType.PNG
        options.ZoomType = DB.ZoomFitType.FitToPage
        options.PixelSize = 1280
        options.ImageResolution = map_dpi(resolution)
        options.SetViewsAndSheets([view.Id])
        doc.ExportImage(options)

        prefix = os.path.basename(export_base)
        image_path = next(
            (
                os.path.join(temp_dir, name)
                for name in sorted(list_directory_names(temp_dir))
                if name.startswith(prefix)
            ),
            None,
        )
        if image_path is None:
            raise ToolError("View capture completed but no image file was produced")

        with open(image_path, "rb") as stream:
            data = stream.read()
        return ViewCaptureResult(
            data=data,
            viewName=view.Name,
            viewId=int(view.Id.Value),
            imagePath=image_path,
        )

    @staticmethod
    def preview_schedule(
        schedule_id: int, max_rows: int = 30
    ) -> SchedulePreviewResult:
        doc = require_doc()
        schedule = doc.GetElement(DB.ElementId(schedule_id))
        if not isinstance(schedule, DB.ViewSchedule):
            raise ToolError(f"Schedule {schedule_id} not found")

        columns, all_rows = _read_schedule_table(schedule)
        limit = max_rows if max_rows > 0 else 30
        preview_rows = all_rows[:limit]
        csv_text = _build_csv(columns, preview_rows)
        return SchedulePreviewResult(
            scheduleName=schedule.Name,
            csvText=csv_text,
            embeddedRows=len(preview_rows),
            totalRows=len(all_rows),
            columnCount=len(columns),
        )

    @staticmethod
    def model_digest() -> ModelDigestResult:
        doc = require_doc()
        return ModelDigestResult(
            projectTitle=doc.Title,
            viewCount=count_user_views(doc),
            levelCount=count_levels(doc),
            warningCount=count_warnings(doc),
        )


def _read_schedule_table(
    schedule: DB.ViewSchedule,
) -> tuple[list[str], list[dict[str, str]]]:
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
        columns = [f"Column_{i + 1}" for i in range(column_count)]

    rows: list[dict[str, str]] = []
    for row_index in range(body.NumberOfRows):
        row: dict[str, str] = {}
        for col in range(column_count):
            row[columns[col]] = schedule.GetCellText(
                DB.SectionType.Body, row_index, col
            )
        rows.append(row)
    return columns, rows


class _TextBuffer:
    def __init__(self) -> None:
        self._parts: list[str] = []

    def write(self, text: str) -> int:
        self._parts.append(text)
        return len(text)

    def getvalue(self) -> str:
        return "".join(self._parts)


def _build_csv(columns: list[str], rows: list[dict[str, str]]) -> str:
    buffer = _TextBuffer()
    writer = csv.writer(buffer)
    writer.writerow(columns)
    for row in rows:
        writer.writerow([row.get(column, "") for column in columns])
    return buffer.getvalue()
