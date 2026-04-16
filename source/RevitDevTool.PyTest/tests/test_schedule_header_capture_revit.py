import json

import pytest

from tests.schedule.constants import SOURCE_SCHEDULE_ID
from tests.schedule.constants import TARGET_SCHEDULE_ID
from tests.schedule.model import get_schedule
from tests.schedule.model import get_section


pytestmark = pytest.mark.usefixtures("revit_auto_rollback")


def _color_signature(color):
    if color is None:
        return None
    return {
        "red": color.Red,
        "green": color.Green,
        "blue": color.Blue,
    }


def _style_signature(style):
    if style is None:
        return None
    return {
        "font_name": style.FontName,
        "text_size": style.TextSize,
        "text_orientation": style.TextOrientation,
        "is_bold": style.IsFontBold,
        "is_italic": style.IsFontItalic,
        "is_underline": style.IsFontUnderline,
        "horizontal_alignment": str(style.FontHorizontalAlignment),
        "vertical_alignment": str(style.FontVerticalAlignment),
        "text_color": _color_signature(style.TextColor),
        "background_color": _color_signature(style.BackgroundColor),
        "sheet_background_color": _color_signature(style.SheetBackgroundColor),
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


def _format_signature(format_options):
    if format_options is None:
        return None
    use_default = format_options.UseDefault
    return {
        "use_default": use_default,
        "accuracy": None if use_default else format_options.Accuracy,
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


def _capture_header(schedule):
    header = get_section(schedule, "Header")
    title = get_section(schedule, "Title")
    body = get_section(schedule, "Body")
    rows = []
    for row in range(header.FirstRowNumber, header.LastRowNumber + 1):
        row_cells = []
        col = header.FirstColumnNumber
        while col <= header.LastColumnNumber:
            merged = header.GetMergedCell(row, col)
            left = col
            right = col
            top = row
            bottom = row
            if merged is not None:
                top = merged.Top
                left = merged.Left
                bottom = merged.Bottom
                right = merged.Right

            style = header.GetTableCellStyle(row, col)
            format_options = header.GetCellFormatOptions(row, col, schedule.Document)
            row_cells.append(
                {
                    "anchor": {"row": row, "col": col},
                    "merge": {
                        "top": top,
                        "left": left,
                        "bottom": bottom,
                        "right": right,
                    },
                    "text": header.GetCellText(row, col),
                    "column_width": header.GetColumnWidth(col),
                    "row_height": header.GetRowHeight(row),
                    "style": _style_signature(style),
                    "format": _format_signature(format_options),
                }
            )
            col = max(right + 1, col + 1)
        rows.append({"row": row, "cells": row_cells})

    return {
        "name": schedule.Name,
        "header": {
            "first_row": header.FirstRowNumber,
            "last_row": header.LastRowNumber,
            "number_of_rows": header.NumberOfRows,
            "first_col": header.FirstColumnNumber,
            "last_col": header.LastColumnNumber,
            "number_of_cols": header.NumberOfColumns,
        },
        "title": {
            "first_row": title.FirstRowNumber,
            "last_row": title.LastRowNumber,
            "number_of_rows": title.NumberOfRows,
            "first_col": title.FirstColumnNumber,
            "last_col": title.LastColumnNumber,
            "number_of_cols": title.NumberOfColumns,
        },
        "body": {
            "first_row": body.FirstRowNumber,
            "last_row": body.LastRowNumber,
            "number_of_rows": body.NumberOfRows,
            "first_col": body.FirstColumnNumber,
            "last_col": body.LastColumnNumber,
            "number_of_cols": body.NumberOfColumns,
        },
        "rows": rows,
    }


def test_capture_air_terminal_schedule_headers(revit_doc):
    source = get_schedule(revit_doc, SOURCE_SCHEDULE_ID)
    target = get_schedule(revit_doc, TARGET_SCHEDULE_ID)

    payload = {
        "source": _capture_header(source),
        "target": _capture_header(target),
    }

    print(json.dumps(payload, ensure_ascii=False, indent=2))
