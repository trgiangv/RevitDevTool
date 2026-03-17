"""Service for exporting Revit model data to Excel using Polars + xlsxwriter."""
from __future__ import annotations

import os
import tempfile

import polars as pl
import xlsxwriter
from Autodesk.Revit import DB

from dto.export import ExportResult, ScheduleExportResult
from dto.filters import FilteredExportResult, FilterRequest, QueryElementsResult
from services.filter_service import FilterService
from shared.element_helpers import (
    category_display_name,
    element_id_value,
    find_category_by_name,
    normalize_string,
    param_value_as_string,
    require_doc,
)


class ExportService:
    def export_elements_to_excel(
        self,
        categories: list[str],
        parameters: list[str] | None = None,
        output_path: str | None = None,
    ) -> ExportResult:
        """Export elements from specified categories to an Excel file."""
        doc = require_doc()

        rows: list[dict] = []
        exported_categories: list[str] = []

        for cat_name in categories:
            category = find_category_by_name(doc, cat_name)
            if category is None:
                continue

            elements = (
                DB.FilteredElementCollector(doc)
                .OfCategoryId(category.Id)
                .WhereElementIsNotElementType()
                .ToElements()
            )

            for elem in elements:
                row = self._extract_element_row(doc, elem, parameters)
                rows.append(row)

            exported_categories.append(normalize_string(cat_name))

        if not rows:
            raise ValueError("No elements found in categories: {}".format(", ".join(categories)))

        df = pl.DataFrame(rows)
        file_path = output_path or self._default_export_path("elements_export.xlsx")
        _write_dataframe_to_xlsx(df, file_path, "Elements")
        file_size = os.path.getsize(file_path)

        return ExportResult(
            file_path=file_path,
            format="xlsx",
            row_count=len(df),
            column_count=len(df.columns),
            categories_exported=exported_categories,
            file_size_bytes=file_size,
        )

    def export_schedule_to_excel(
        self,
        schedule_name: str,
        output_path: str | None = None,
    ) -> ScheduleExportResult:
        """Export a Revit schedule view to Excel."""
        doc = require_doc()

        schedule = self._find_schedule(doc, schedule_name)
        if schedule is None:
            raise ValueError("Schedule '{}' not found".format(schedule_name))

        table_data = schedule.GetTableData()
        section = table_data.GetSectionData(DB.SectionType.Body)
        rows_count = section.NumberOfRows
        cols_count = section.NumberOfColumns

        headers: list[str] = []
        header_section = table_data.GetSectionData(DB.SectionType.Header)
        if header_section.NumberOfRows > 0:
            for col in range(cols_count):
                headers.append(schedule.GetCellText(DB.SectionType.Header, 0, col))
        else:
            headers = ["Column_{}".format(i) for i in range(cols_count)]

        data_rows: list[dict] = []
        for row in range(rows_count):
            data_row: dict[str, str] = {}
            for col in range(cols_count):
                data_row[headers[col]] = schedule.GetCellText(DB.SectionType.Body, row, col)
            data_rows.append(data_row)

        df = pl.DataFrame(data_rows)
        file_path = output_path or self._default_export_path("{}.xlsx".format(schedule_name))
        _write_dataframe_to_xlsx(df, file_path, schedule_name[:31])
        file_size = os.path.getsize(file_path)

        return ScheduleExportResult(
            file_path=file_path,
            schedule_name=schedule_name,
            row_count=len(df),
            column_count=len(df.columns),
            file_size_bytes=file_size,
        )

    @staticmethod
    def _find_schedule(doc: DB.Document, schedule_name: str) -> DB.ViewSchedule | None:
        target = normalize_string(schedule_name)
        for view in DB.FilteredElementCollector(doc).OfClass(DB.ViewSchedule):
            if normalize_string(view.Name) == target:
                return view
        return None

    @staticmethod
    def _extract_element_row(doc: DB.Document, elem: DB.Element, parameters: list[str] | None) -> dict:
        row: dict[str, object] = {
            "ElementId": element_id_value(elem.Id),
            "Name": normalize_string(elem.Name),
            "Category": category_display_name(elem),
        }

        if parameters:
            ExportService._extract_named_params(doc, elem, parameters, row)
        else:
            ExportService._extract_all_params(doc, elem, row)

        return row

    @staticmethod
    def _extract_named_params(doc: DB.Document, elem: DB.Element, parameters: list[str], row: dict) -> None:
        for param_name in parameters:
            param = elem.LookupParameter(param_name)
            if param is not None and param.HasValue:
                row[param_name] = param_value_as_string(param, doc)
            else:
                row[param_name] = ""

    @staticmethod
    def _extract_all_params(doc: DB.Document, elem: DB.Element, row: dict) -> None:
        """Extract all parameter values via ParametersMap, merging instance + type params."""
        seen: set[str] = set()
        _collect_params_from_map(elem.ParametersMap, doc, row, seen)
        try:
            type_id = elem.GetTypeId()
            type_elem = doc.GetElement(type_id) if type_id and type_id != DB.ElementId.InvalidElementId else None
        except Exception:
            type_elem = None
        if type_elem is not None:
            _collect_params_from_map(type_elem.ParametersMap, doc, row, seen)

    def export_filtered_elements(
        self,
        filter_request: FilterRequest,
        parameters: list[str] | None = None,
        output_path: str | None = None,
    ) -> FilteredExportResult:
        """Export elements matching a declarative filter to Excel."""
        doc = require_doc()
        filter_svc = FilterService()
        elements = filter_svc.collect_elements(filter_request)

        if not elements:
            raise ValueError("No elements match the specified filters")

        rows: list[dict] = []
        for elem in elements:
            rows.append(self._extract_element_row(doc, elem, parameters))

        df = pl.DataFrame(rows)
        file_path = output_path or self._default_export_path("filtered_export.xlsx")
        _write_dataframe_to_xlsx(df, file_path, "FilteredElements")
        file_size = os.path.getsize(file_path)

        return FilteredExportResult(
            file_path=file_path,
            format="xlsx",
            row_count=len(df),
            column_count=len(df.columns),
            filter_summary=filter_svc.describe_filters(filter_request),
            file_size_bytes=file_size,
        )

    def query_elements(
        self,
        filter_request: FilterRequest,
        sample_size: int = 20,
    ) -> QueryElementsResult:
        """Query elements matching a filter and return summary info (no file export)."""
        filter_svc = FilterService()
        elements = filter_svc.collect_elements(filter_request)

        by_category: dict[str, int] = {}
        for elem in elements:
            cat_name = category_display_name(elem)
            by_category[cat_name] = by_category.get(cat_name, 0) + 1

        sample: list[dict] = []
        for elem in elements[:sample_size]:
            sample.append({
                "ElementId": element_id_value(elem.Id),
                "Name": normalize_string(elem.Name),
                "Category": category_display_name(elem),
            })

        return QueryElementsResult(
            total_elements=len(elements),
            by_category=by_category,
            sample_elements=sample,
            filter_summary=filter_svc.describe_filters(filter_request),
        )

    @staticmethod
    def _default_export_path(filename: str) -> str:
        folder = os.path.join(tempfile.gettempdir(), "RevitMCPExports")
        os.makedirs(folder, exist_ok=True)
        return os.path.join(folder, filename)


def _collect_params_from_map(
    params_map: object, doc: DB.Document, row: dict, seen: set[str],
) -> None:
    """Iterate a ParametersMap and add values to row, skipping already-seen names."""
    for param in params_map:
        try:
            name = normalize_string(param.Definition.Name)
            if name and name not in seen and param.HasValue:
                row[name] = param_value_as_string(param, doc)
                seen.add(name)
        except Exception:
            continue


def _write_dataframe_to_xlsx(df: pl.DataFrame, file_path: str, sheet_name: str) -> None:
    """Write a Polars DataFrame to .xlsx via xlsxwriter directly.

    polars.write_excel() silently drops data rows on sparse DataFrames
    with many null columns, so we bypass it entirely.
    """
    os.makedirs(os.path.dirname(file_path) or ".", exist_ok=True)
    wb = xlsxwriter.Workbook(file_path)
    ws = wb.add_worksheet(sheet_name)
    header_fmt = wb.add_format({"bold": True})

    columns = df.columns
    for col_idx, col_name in enumerate(columns):
        ws.write(0, col_idx, col_name, header_fmt)

    for row_idx in range(df.height):
        for col_idx, col_name in enumerate(columns):
            val = df[row_idx, col_idx]
            if val is None:
                continue
            ws.write(row_idx + 1, col_idx, val)

    ws.autofilter(0, 0, df.height, len(columns) - 1)
    wb.close()
