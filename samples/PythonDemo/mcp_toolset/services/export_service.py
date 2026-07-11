"""Export services: PDF, image, Excel, schedule."""
from __future__ import annotations

import os
import tempfile
from datetime import datetime

import polars as pl
import xlsxwriter
from Autodesk.Revit import DB

from dto.export import ExportPdfResult, ExportImageResult, ExportResult, ScheduleExportResult
from dto.filters import FilterSpec
from services.filter_service import FilterService
from shared.element_helpers import (
    category_display_name,
    element_id_value,
    normalize_string,
    param_value_as_string,
    require_doc,
)
from shared.path_guard import default_export_dir, generate_unique_file_path, sanitize_directory_path, sanitize_file_path
from shared.responses import ToolError


class ExportService:
    def __init__(self) -> None:
        self._filters = FilterService()

    def export_to_excel(
        self,
        filters: FilterSpec | None = None,
        parameters: list[str] | None = None,
        output_path: str | None = None,
    ) -> ExportResult:
        doc = require_doc()
        elements = self._filters.collect_elements(filters)
        if not elements:
            raise ToolError("No elements match the specified filters")

        rows = [self._extract_row(doc, elem, parameters) for elem in elements]
        df = pl.DataFrame(rows)
        file_path = output_path or os.path.join(default_export_dir(), "filtered_export.xlsx")
        file_path = sanitize_file_path(file_path)
        _write_dataframe_to_xlsx(df, file_path, "FilteredElements")
        return ExportResult(
            file_path=file_path,
            format="xlsx",
            row_count=len(df),
            column_count=len(df.columns),
            categories_exported=[],
            file_size_bytes=os.path.getsize(file_path),
        )

    def export_schedule(
        self,
        schedule_id: int,
        format: str = "xlsx",
        output_path: str | None = None,
    ) -> ScheduleExportResult:
        doc = require_doc()
        schedule = doc.GetElement(DB.ElementId(schedule_id))
        if not isinstance(schedule, DB.ViewSchedule):
            raise ToolError("Schedule {} not found".format(schedule_id))

        table_data = schedule.GetTableData()
        body = table_data.GetSectionData(DB.SectionType.Body)
        rows_count = body.NumberOfRows
        cols_count = body.NumberOfColumns
        headers = []
        header_section = table_data.GetSectionData(DB.SectionType.Header)
        if header_section.NumberOfRows > 0:
            for col in range(cols_count):
                headers.append(schedule.GetCellText(DB.SectionType.Header, 0, col))
        else:
            headers = ["Column_{}".format(i) for i in range(cols_count)]

        data_rows = []
        for row in range(rows_count):
            data_row = {}
            for col in range(cols_count):
                data_row[headers[col]] = schedule.GetCellText(DB.SectionType.Body, row, col)
            data_rows.append(data_row)

        df = pl.DataFrame(data_rows)
        ext = "xlsx" if format.lower() == "xlsx" else "csv"
        file_path = output_path or os.path.join(default_export_dir(), "{}.{}".format(schedule.Name, ext))
        file_path = sanitize_file_path(file_path)
        if ext == "xlsx":
            _write_dataframe_to_xlsx(df, file_path, schedule.Name[:31])
        else:
            df.write_csv(file_path)

        return ScheduleExportResult(
            file_path=file_path,
            schedule_name=normalize_string(schedule.Name),
            row_count=len(df),
            column_count=len(df.columns),
            file_size_bytes=os.path.getsize(file_path),
        )

    def export_pdf(
        self,
        view_ids: list[int] | None = None,
        directory: str | None = None,
        combine_into_single: bool = False,
    ) -> ExportPdfResult:
        doc = require_doc()
        output_dir = sanitize_directory_path(directory) if directory else tempfile.gettempdir()
        os.makedirs(output_dir, exist_ok=True)
        resolved = self._resolve_view_ids(doc, view_ids)
        options = DB.PDFExportOptions()
        options.ExportQuality = DB.PDFExportQualityType.DPI300
        options.ZoomType = DB.ZoomType.Zoom
        options.ZoomPercentage = 100
        options.Combine = combine_into_single
        if combine_into_single:
            options.FileName = os.path.basename(
                generate_unique_file_path(output_dir, doc.Title, "pdf")
            )
        ids = [DB.ElementId(v) for v in resolved]
        if not doc.Export(output_dir, ids, options):
            raise ToolError("PDF export failed")
        return ExportPdfResult(file_paths=[output_dir], page_count=len(resolved))

    def export_image(
        self,
        view_ids: list[int] | None = None,
        format: str = "png",
        directory: str | None = None,
        resolution: int = 150,
    ) -> ExportImageResult:
        doc = require_doc()
        output_dir = sanitize_directory_path(directory) if directory else tempfile.gettempdir()
        os.makedirs(output_dir, exist_ok=True)
        resolved = self._resolve_view_ids(doc, view_ids)
        image_type = self._parse_image_format(format)
        export_base = os.path.join(output_dir, "export_{}".format(datetime.now().strftime("%Y%m%d_%H%M%S")))
        options = DB.ImageExportOptions()
        options.ExportRange = DB.ExportRange.SetOfViews
        options.FilePath = export_base
        options.HLRandWFViewsFileType = image_type
        options.ShadowViewsFileType = image_type
        options.ZoomType = DB.ZoomFitType.FitToPage
        options.PixelSize = 1024
        options.SetViewsAndSheets([DB.ElementId(v) for v in resolved])
        doc.ExportImage(options)
        files = [os.path.join(output_dir, f) for f in os.listdir(output_dir) if f.startswith(os.path.basename(export_base))]
        return ExportImageResult(file_paths=files or [export_base])

    @staticmethod
    def _resolve_view_ids(doc: DB.Document, view_ids: list[int] | None) -> list[int]:
        if view_ids:
            return view_ids
        view = doc.ActiveView
        if view is None:
            raise ToolError("No active view")
        return [element_id_value(view.Id)]

    @staticmethod
    def _parse_image_format(format: str) -> DB.ImageFileType:
        normalized = format.strip().lower()
        if normalized in ("jpg", "jpeg"):
            return DB.ImageFileType.JPEGLossless
        if normalized == "bmp":
            return DB.ImageFileType.BMP
        return DB.ImageFileType.PNG

    @staticmethod
    def _extract_row(doc: DB.Document, elem: DB.Element, parameters: list[str] | None) -> dict:
        row = {
            "ElementId": element_id_value(elem.Id),
            "Name": normalize_string(elem.Name),
            "Category": category_display_name(elem),
        }
        if parameters:
            for name in parameters:
                param = elem.LookupParameter(name)
                row[name] = param_value_as_string(param, doc) if param and param.HasValue else ""
        else:
            seen: set[str] = set()
            for param in elem.ParametersMap:
                try:
                    pname = normalize_string(param.Definition.Name)
                    if pname and pname not in seen and param.HasValue:
                        row[pname] = param_value_as_string(param, doc)
                        seen.add(pname)
                except Exception:
                    continue
        return row


def _write_dataframe_to_xlsx(df: pl.DataFrame, file_path: str, sheet_name: str) -> None:
    os.makedirs(os.path.dirname(file_path) or ".", exist_ok=True)
    wb = xlsxwriter.Workbook(file_path)
    ws = wb.add_worksheet(sheet_name[:31])
    header_fmt = wb.add_format({"bold": True})
    columns = df.columns
    for col_idx, col_name in enumerate(columns):
        ws.write(0, col_idx, col_name, header_fmt)
    for row_idx in range(df.height):
        for col_idx, col_name in enumerate(columns):
            val = df[row_idx, col_idx]
            if val is not None:
                ws.write(row_idx + 1, col_idx, val)
    ws.autofilter(0, 0, df.height, len(columns) - 1)
    wb.close()
