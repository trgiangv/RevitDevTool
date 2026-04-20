"""Full schedule state serializer for snapshot-based Revit tests."""

import copy
import json
import os

from tests.schedule.model import element_id_value
from tests.schedule.model import get_section
from tests.schedule.model import table_section_names


SNAPSHOT_FILE = os.path.join(os.path.dirname(__file__), "test.json")


def color_signature(color):
    if color is None:
        return None
    return {
        "red": color.Red,
        "green": color.Green,
        "blue": color.Blue,
    }


def style_signature(style):
    if style is None:
        return None
    return {
        "font_name": style.FontName,
        "text_size": None if style.TextSize is None else round(style.TextSize, 6),
        "text_orientation": style.TextOrientation,
        "is_bold": style.IsFontBold,
        "is_italic": style.IsFontItalic,
        "is_underline": style.IsFontUnderline,
        "horizontal_alignment": str(style.FontHorizontalAlignment),
        "vertical_alignment": str(style.FontVerticalAlignment),
        "text_color": color_signature(style.TextColor),
        "background_color": color_signature(style.BackgroundColor),
        "sheet_background_color": color_signature(style.SheetBackgroundColor),
        "border_top": None
        if style.BorderTopLineStyle is None
        else str(style.BorderTopLineStyle),
        "border_bottom": None
        if style.BorderBottomLineStyle is None
        else str(style.BorderBottomLineStyle),
        "border_left": None
        if style.BorderLeftLineStyle is None
        else str(style.BorderLeftLineStyle),
        "border_right": None
        if style.BorderRightLineStyle is None
        else str(style.BorderRightLineStyle),
    }


def format_signature(format_options):
    if format_options is None:
        return None
    use_default = format_options.UseDefault
    return {
        "use_default": use_default,
        "accuracy": None
        if use_default or format_options.Accuracy is None
        else round(format_options.Accuracy, 6),
        "suppress_leading_zeros": None
        if use_default
        else format_options.SuppressLeadingZeros,
        "suppress_trailing_zeros": None
        if use_default
        else format_options.SuppressTrailingZeros,
        "suppress_spaces": None if use_default else format_options.SuppressSpaces,
        "use_digit_grouping": None if use_default else format_options.UseDigitGrouping,
        "use_plus_prefix": None if use_default else format_options.UsePlusPrefix,
    }


def merged_signature(merged):
    if merged is None:
        return None
    return {
        "top": merged.Top,
        "left": merged.Left,
        "bottom": merged.Bottom,
        "right": merged.Right,
    }


def section_summary(section):
    return {
        "first_row": section.FirstRowNumber,
        "last_row": section.LastRowNumber,
        "number_of_rows": section.NumberOfRows,
        "first_col": section.FirstColumnNumber,
        "last_col": section.LastColumnNumber,
        "number_of_cols": section.NumberOfColumns,
    }


def serialize_header_section(schedule):
    section = get_section(schedule, "Header")
    rows = []
    for row in range(section.FirstRowNumber, section.LastRowNumber + 1):
        cells = []
        for col in range(section.FirstColumnNumber, section.LastColumnNumber + 1):
            try:
                style = section.GetTableCellStyle(row, col)
            except Exception:
                style = None
            try:
                format_options = section.GetCellFormatOptions(row, col, schedule.Document)
            except Exception:
                format_options = None
            try:
                merged = section.GetMergedCell(row, col)
            except Exception:
                merged = None
            try:
                text = section.GetCellText(row, col)
            except Exception:
                text = None
            try:
                column_width = section.GetColumnWidth(col)
            except Exception:
                column_width = None
            try:
                row_height = section.GetRowHeight(row)
            except Exception:
                row_height = None

            cells.append(
                {
                    "row": row,
                    "col": col,
                    "text": text,
                    "merged": merged_signature(merged),
                    "column_width": None
                    if column_width is None
                    else round(column_width, 6),
                    "row_height": None if row_height is None else round(row_height, 6),
                    "style": style_signature(style),
                    "format": format_signature(format_options),
                }
            )
        rows.append({"row": row, "cells": cells})

    return {
        "summary": section_summary(section),
        "rows": rows,
    }


def serialize_body_structure(schedule):
    section = get_section(schedule, "Body")
    columns = []
    for col in range(section.FirstColumnNumber, section.LastColumnNumber + 1):
        try:
            column_width = section.GetColumnWidth(col)
        except Exception:
            column_width = None
        columns.append(
            {
                "col": col,
                "column_width": None
                if column_width is None
                else round(column_width, 6),
            }
        )

    return {
        "summary": section_summary(section),
        "columns": columns,
    }


def serialize_section_summary(schedule, section_name):
    section = get_section(schedule, section_name)
    return section_summary(section)


def serialize_fields(schedule):
    definition = schedule.Definition

    fields = []
    try:
        field_order = list(definition.GetFieldOrder() or [])
    except Exception:
        field_order = []

    for index, field_id in enumerate(field_order):
        try:
            field = definition.GetField(field_id)
        except Exception:
            field = None
        if field is None:
            continue

        try:
            parameter_id = element_id_value(field.ParameterId)
        except Exception:
            parameter_id = None
        try:
            field_name = field.GetName(schedule.Document)
        except Exception:
            field_name = None
        try:
            heading = field.ColumnHeading
        except Exception:
            heading = None
        try:
            is_hidden = field.IsHidden
        except Exception:
            is_hidden = None
        try:
            style = style_signature(field.GetStyle())
        except Exception:
            style = None
        try:
            format_options = format_signature(field.GetFormatOptions())
        except Exception:
            format_options = None
        try:
            grid_column_width = field.GridColumnWidth
        except Exception:
            grid_column_width = None

        fields.append(
            {
                "index": index,
                "parameter_id": parameter_id,
                "field_name": field_name,
                "column_heading": heading,
                "is_hidden": is_hidden,
                "style": style,
                "format": format_options,
                "grid_column_width": None
                if grid_column_width is None
                else round(grid_column_width, 6),
            }
        )

    return fields


def serialize_schedule(schedule):
    document_title = os.path.basename(schedule.Document.Title or "")
    section_names = table_section_names(schedule)
    payload = {
        "schedule_id": element_id_value(schedule.Id),
        "schedule_name": schedule.Name,
        "document_title": document_title,
        "is_template": bool(schedule.IsTemplate),
        "header_text_type_id": element_id_value(schedule.HeaderTextTypeId),
        "title_text_type_id": element_id_value(schedule.TitleTextTypeId),
        "view_template_id": element_id_value(schedule.ViewTemplateId),
        "fields": serialize_fields(schedule),
        "sections": {},
    }
    for section_name in section_names:
        if section_name == "Header":
            payload["sections"][section_name] = serialize_header_section(schedule)
        elif section_name == "Body":
            payload["sections"][section_name] = serialize_body_structure(schedule)
        else:
            payload["sections"][section_name] = {
                "summary": serialize_section_summary(schedule, section_name)
            }
    return payload


def schedule_snapshot_key(schedule):
    document_title = os.path.basename(schedule.Document.Title or "")
    return "%s_%s_%s" % (
        element_id_value(schedule.Id),
        schedule.Name,
        document_title,
    )


def serialize_schedule_list(schedules):
    payload = {}
    for schedule in schedules:
        payload[schedule_snapshot_key(schedule)] = serialize_schedule(schedule)
    return payload


def write_snapshot_file(payload, file_path=SNAPSHOT_FILE):
    with open(file_path, "w") as stream:
        json.dump(payload, stream, ensure_ascii=False, indent=2)


def load_snapshot_file(file_path=SNAPSHOT_FILE):
    with open(file_path, "r") as stream:
        return json.load(stream)


def deep_copy_snapshot(payload):
    return copy.deepcopy(payload)
