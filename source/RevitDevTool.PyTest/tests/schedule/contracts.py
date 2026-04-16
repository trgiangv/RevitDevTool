"""Header contract builders and assertions for schedule formatting tests."""

import copy

from tests.schedule.model import get_section


class HeaderContractInspector(object):
    def __init__(self, schedule):
        self.schedule = schedule
        self.header = get_section(schedule, "Header")

    def visible_last_column(self):
        body = get_section(self.schedule, "Body")
        return min(self.header.LastColumnNumber, body.LastColumnNumber)

    def merged_cell_signature(self, merged_cell):
        if merged_cell is None:
            return None
        return {
            "top": merged_cell.Top,
            "left": merged_cell.Left,
            "bottom": merged_cell.Bottom,
            "right": merged_cell.Right,
        }

    def is_nontrivial_merged_signature(self, signature):
        if signature is None:
            return False
        return not (
            signature["top"] == signature["bottom"]
            and signature["left"] == signature["right"]
        )

    def row_spans(self, row):
        spans = []
        col = self.header.FirstColumnNumber
        visible_last_col = self.visible_last_column()
        while col <= visible_last_col:
            signature = self.merged_cell_signature(self.header.GetMergedCell(row, col))
            if self.is_nontrivial_merged_signature(signature):
                signature["right"] = min(signature["right"], visible_last_col)
                spans.append(signature)
                col = signature["right"] + 1
                continue

            spans.append(
                {
                    "top": row,
                    "left": col,
                    "bottom": row,
                    "right": col,
                }
            )
            col += 1

        return spans

    def nonempty_visual_cell_count(self, row):
        count = 0
        for span in self.row_spans(row):
            text = self.header.GetCellText(row, span["left"])
            if text not in (None, ""):
                count += 1
        return count

    def color_signature(self, color):
        if color is None:
            return None
        return {
            "red": color.Red,
            "green": color.Green,
            "blue": color.Blue,
        }

    def style_signature(self, style):
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
            "text_color": self.color_signature(style.TextColor),
            "background_color": self.color_signature(style.BackgroundColor),
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

    def format_signature(self, format_options):
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
            "use_digit_grouping": None
            if use_default
            else format_options.UseDigitGrouping,
            "use_plus_prefix": None if use_default else format_options.UsePlusPrefix,
        }

    def title_last_row(self):
        return self.header.LastRowNumber

    def span_width(self, span):
        total_width = 0.0
        for col in range(span["left"], span["right"] + 1):
            width = self.header.GetColumnWidth(col)
            if width is not None:
                total_width += width
        return round(total_width, 9)

    def visual_cells(self, row_count):
        cells = []
        first_row = self.header.FirstRowNumber
        last_row = min(self.header.LastRowNumber, first_row + row_count - 1)
        for row in range(first_row, last_row + 1):
            row_cells = []
            for span in self.row_spans(row):
                left = span["left"]
                row_cells.append(
                    {
                        "row": row,
                        "left": left,
                        "right": span["right"],
                        "text": self.header.GetCellText(row, left),
                        "width": self.span_width(span),
                        "height": self.header.GetRowHeight(row),
                        "style": self.style_signature(
                            self.header.GetTableCellStyle(row, left)
                        ),
                        "format": self.format_signature(
                            self.header.GetCellFormatOptions(
                                row, left, self.schedule.Document
                            )
                        ),
                    }
                )
            cells.append(row_cells)
        return cells

    def normalized_visual_cells(self, cells):
        normalized = []
        for row_cells in cells:
            normalized_row = []
            for cell in row_cells:
                normalized_row.append(
                    {
                        "left": cell["left"],
                        "right": cell["right"],
                        "text": cell["text"],
                        "width": round(cell["width"], 6),
                        "height": None
                        if cell["height"] is None
                        else round(cell["height"], 6),
                        "style": cell["style"],
                        "format": cell["format"],
                    }
                )
            normalized.append(normalized_row)
        return normalized

    def build_contract(self):
        title_last_row = self.title_last_row()
        assert title_last_row is not None
        row_count = title_last_row - self.header.FirstRowNumber + 1
        visual_cells = self.normalized_visual_cells(self.visual_cells(row_count))
        has_text = any(
            cell["text"] not in (None, "") for row in visual_cells for cell in row
        )
        return {
            "title_last_row": title_last_row,
            "row_count": row_count,
            "visual_cells": visual_cells,
            "has_text": has_text,
        }

    @staticmethod
    def assert_visual_cells_match(actual_cells, expected_cells):
        assert len(actual_cells) == len(expected_cells), {
            "actual_row_count": len(actual_cells),
            "expected_row_count": len(expected_cells),
            "actual": actual_cells,
            "expected": expected_cells,
        }

        for row_index, expected_row in enumerate(expected_cells):
            actual_row = actual_cells[row_index]
            assert len(actual_row) == len(expected_row), {
                "row": row_index,
                "actual_cell_count": len(actual_row),
                "expected_cell_count": len(expected_row),
                "actual_row": actual_row,
                "expected_row": expected_row,
            }
            for cell_index, expected_cell in enumerate(expected_row):
                actual_cell = actual_row[cell_index]
                assert actual_cell["text"] == expected_cell["text"], {
                    "row": row_index,
                    "cell": cell_index,
                    "field": "text",
                    "actual": actual_cell["text"],
                    "expected": expected_cell["text"],
                }
                assert actual_cell["width"] == expected_cell["width"], {
                    "row": row_index,
                    "cell": cell_index,
                    "field": "width",
                    "actual": actual_cell["width"],
                    "expected": expected_cell["width"],
                }
                assert actual_cell["height"] == expected_cell["height"], {
                    "row": row_index,
                    "cell": cell_index,
                    "field": "height",
                    "actual": actual_cell["height"],
                    "expected": expected_cell["height"],
                }
                assert actual_cell["style"] == expected_cell["style"], {
                    "row": row_index,
                    "cell": cell_index,
                    "field": "style",
                    "actual": actual_cell["style"],
                    "expected": expected_cell["style"],
                }
                assert actual_cell["format"] == expected_cell["format"], {
                    "row": row_index,
                    "cell": cell_index,
                    "field": "format",
                    "actual": actual_cell["format"],
                    "expected": expected_cell["format"],
                }


def build_header_contract(schedule):
    return HeaderContractInspector(schedule).build_contract()


def get_title_header_last_row(section):
    inspector = HeaderContractInspector.__new__(HeaderContractInspector)
    inspector.schedule = None
    inspector.header = section
    return inspector.title_last_row()


def header_row_spans(schedule, row):
    return HeaderContractInspector(schedule).row_spans(row)


def span_width(section, span):
    inspector = HeaderContractInspector.__new__(HeaderContractInspector)
    inspector.schedule = None
    inspector.header = section
    return inspector.span_width(span)


def assert_header_matches_source(source_schedule, target_schedule):
    source_inspector = HeaderContractInspector(source_schedule)
    target_inspector = HeaderContractInspector(target_schedule)

    source_contract = source_inspector.build_contract()
    assert target_inspector.title_last_row() == source_contract["title_last_row"]

    actual_visual_cells = target_inspector.normalized_visual_cells(
        target_inspector.visual_cells(source_contract["row_count"])
    )
    HeaderContractInspector.assert_visual_cells_match(
        actual_visual_cells,
        source_contract["visual_cells"],
    )
    return source_contract


def is_schedule_title_text(schedule, text):
    if text in (None, ""):
        return False

    schedule_name = schedule.Name.strip().lower()
    normalized_text = text.strip().lower()
    if normalized_text == schedule_name:
        return True
    if normalized_text == "<%s>" % schedule_name:
        return True
    return False


def target_schedule_title_text(source_schedule, target_schedule, source_text):
    if source_text in (None, ""):
        return source_text

    normalized_text = source_text.strip().lower()
    source_name = source_schedule.Name.strip().lower()
    target_name = target_schedule.Name.strip()
    if normalized_text == source_name:
        return target_name
    if normalized_text == "<%s>" % source_name:
        return "<%s>" % target_name
    return source_text


def compare_header_contracts(
    source_schedule,
    target_schedule,
    actual_contract,
    expected_contract,
    compared_fields=None,
):
    if compared_fields is None:
        compared_fields = ("text", "style")

    issues = []

    if actual_contract["row_count"] != expected_contract["row_count"]:
        issues.append(
            {
                "field": "row_count",
                "actual": actual_contract["row_count"],
                "expected": expected_contract["row_count"],
            }
        )

    if actual_contract["title_last_row"] != expected_contract["title_last_row"]:
        issues.append(
            {
                "field": "title_last_row",
                "actual": actual_contract["title_last_row"],
                "expected": expected_contract["title_last_row"],
            }
        )

    actual_cells = actual_contract["visual_cells"]
    expected_cells = expected_contract["visual_cells"]
    if len(actual_cells) != len(expected_cells):
        issues.append(
            {
                "field": "row_count_cells",
                "actual": len(actual_cells),
                "expected": len(expected_cells),
            }
        )
        return issues

    for row_index, expected_row in enumerate(expected_cells):
        actual_row = actual_cells[row_index]
        if len(actual_row) != len(expected_row):
            issues.append(
                {
                    "field": "cell_count",
                    "row": row_index,
                    "actual": len(actual_row),
                    "expected": len(expected_row),
                    "actual_row": actual_row,
                    "expected_row": expected_row,
                }
            )
            continue

        for cell_index, expected_cell in enumerate(expected_row):
            actual_cell = actual_row[cell_index]
            expected_text = expected_cell["text"]
            expected_actual_text = expected_text
            if is_schedule_title_text(source_schedule, expected_text):
                expected_actual_text = target_schedule_title_text(
                    source_schedule,
                    target_schedule,
                    expected_text,
                )

            if (
                "text" in compared_fields
                and actual_cell["text"] != expected_actual_text
            ):
                issues.append(
                    {
                        "field": "text",
                        "row": row_index,
                        "cell": cell_index,
                        "actual": actual_cell["text"],
                        "expected": expected_actual_text,
                    }
                )

            for field_name in compared_fields:
                if field_name == "text":
                    continue
                if field_name == "style" and expected_cell["text"] in (None, ""):
                    continue
                if actual_cell[field_name] != expected_cell[field_name]:
                    issues.append(
                        {
                            "field": field_name,
                            "row": row_index,
                            "cell": cell_index,
                            "actual": actual_cell[field_name],
                            "expected": expected_cell[field_name],
                        }
                    )

    return issues


def capture_header_contract(schedule):
    return copy.deepcopy(build_header_contract(schedule))
