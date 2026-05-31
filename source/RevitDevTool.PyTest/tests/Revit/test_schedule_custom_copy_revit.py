import pytest

from tests.schedule.constants import HEADER_COPY_CASES
from tests.schedule.custom_workflow import CustomCopyService
from tests.schedule.custom_workflow import OPTION_COLUMN_GROUPING
from tests.schedule.custom_workflow import OPTION_COLUMN_TITLE
from tests.schedule.field_mapping import FieldMappingService
from tests.schedule.model import get_schedule
from tests.schedule.model import get_section
from tests.schedule.serializer import serialize_fields
from tests.schedule.table_cell import TableCellCopier


pytestmark = pytest.mark.usefixtures("revit_auto_rollback")


def _mapped_column_title_pairs(source_schedule, target_schedule):
    mapping = FieldMappingService(source_schedule, target_schedule).build_mapping()
    source_fields = serialize_fields(source_schedule)
    target_fields = serialize_fields(target_schedule)

    pairs = []
    for match in mapping["matches"]:
        source_index = match["source"]["field_index"]
        target_index = match["target"]["field_index"]
        if source_index >= len(source_fields) or target_index >= len(target_fields):
            continue
        pairs.append(
            (
                source_fields[source_index],
                target_fields[target_index],
            )
        )
    return pairs


def _mutate_target_title_texts(source_schedule, target_schedule):
    mapping = FieldMappingService(source_schedule, target_schedule).build_mapping()
    for index, match in enumerate(mapping["matches"]):
        target_field = match["target"]["field"]
        try:
            target_field.ColumnHeading = "mutated_%s" % index
        except Exception:
            pass


def _mutate_target_title_styles(source_schedule, target_schedule):
    mapping = FieldMappingService(source_schedule, target_schedule).build_mapping()
    for match in mapping["matches"]:
        target_field = match["target"]["field"]
        try:
            style = target_field.GetStyle()
            style.IsFontBold = not style.IsFontBold
            target_field.SetStyle(style)
        except Exception:
            pass


def _ensure_show_headers_enabled(source_schedule, target_schedule):
    source_schedule.Definition.ShowHeaders = True
    target_schedule.Definition.ShowHeaders = True


def _has_source_grouping(schedule):
    return bool(_body_group_spans(schedule))


def _has_two_or_more_mapped_group_columns(source_schedule, target_schedule):
    mapping = FieldMappingService(source_schedule, target_schedule).build_mapping()
    source_to_target = {}
    for match in mapping["matches"]:
        source_to_target[match["source"]["field_index"]] = match["target"]["field_index"]

    body = get_section(source_schedule, "Body")
    title_row = _find_column_title_row(source_schedule, "source")
    for row in range(body.FirstRowNumber, title_row):
        col = body.FirstColumnNumber
        while col <= body.LastColumnNumber:
            merged = body.GetMergedCell(row, col)
            if merged is None or merged.Left != col or merged.Right <= merged.Left:
                col += 1
                continue

            member_indexes = range(
                merged.Left - body.FirstColumnNumber,
                merged.Right - body.FirstColumnNumber + 1,
            )
            matched_target_cols = [
                source_to_target[index]
                for index in member_indexes
                if index in source_to_target
            ]
            if len(matched_target_cols) >= 2:
                return True
            col = merged.Right + 1

    return False


def _body_group_spans(schedule):
    body = get_section(schedule, "Body")
    spans = []
    for row in range(body.FirstRowNumber, body.LastRowNumber + 1):
        col = body.FirstColumnNumber
        while col <= body.LastColumnNumber:
            merged = body.GetMergedCell(row, col)
            if merged is None:
                col += 1
                continue
            if merged.Left != col:
                col += 1
                continue
            if merged.Right <= merged.Left:
                col += 1
                continue
            spans.append(
                {
                    "row": row,
                    "left": merged.Left,
                    "right": merged.Right,
                    "text": body.GetCellText(row, merged.Left),
                }
            )
            col = merged.Right + 1
    return spans


def _body_text_matrix(schedule):
    body = get_section(schedule, "Body")
    rows = []
    for row in range(body.FirstRowNumber, body.LastRowNumber + 1):
        row_texts = []
        for col in range(body.FirstColumnNumber, body.LastColumnNumber + 1):
            try:
                row_texts.append(body.GetCellText(row, col))
            except Exception:
                row_texts.append(None)
        rows.append(row_texts)
    return rows


def _body_group_style_pairs(source_schedule, target_schedule):
    source_body = get_section(source_schedule, "Body")
    target_body = get_section(target_schedule, "Body")
    source_spans = _body_group_spans(source_schedule)
    target_spans = _body_group_spans(target_schedule)
    pairs = []
    for source_span in source_spans:
        matching_target = None
        for target_span in target_spans:
            if target_span["text"] == source_span["text"]:
                matching_target = target_span
                break
        if matching_target is None:
            continue
        source_style = source_body.GetTableCellStyle(source_span["row"], source_span["left"])
        target_style = target_body.GetTableCellStyle(matching_target["row"], matching_target["left"])
        pairs.append(
            (
                {
                    "text": source_span["text"],
                    "is_bold": source_style.IsFontBold,
                    "bg": None if source_style.BackgroundColor is None else (
                        source_style.BackgroundColor.Red,
                        source_style.BackgroundColor.Green,
                        source_style.BackgroundColor.Blue,
                    ),
                },
                {
                    "text": matching_target["text"],
                    "is_bold": target_style.IsFontBold,
                    "bg": None if target_style.BackgroundColor is None else (
                        target_style.BackgroundColor.Red,
                        target_style.BackgroundColor.Green,
                        target_style.BackgroundColor.Blue,
                    ),
                },
            )
        )
    return pairs


def _clear_target_group_headers(schedule):
    body = get_section(schedule, "Body")
    for row in range(body.FirstRowNumber, body.LastRowNumber + 1):
        for col in range(body.FirstColumnNumber, body.LastColumnNumber + 1):
            merged = body.GetMergedCell(row, col)
            if merged is None:
                continue
            if merged.Left != col:
                continue
            if merged.Right <= merged.Left:
                continue
            try:
                from Autodesk.Revit import DB

                body.SetMergedCell(
                    row,
                    col,
                    DB.TableMergedCell(row, col, row, col),
                )
            except Exception:
                pass


def _find_column_title_row(schedule, mapping_side):
    body = get_section(schedule, "Body")
    first_col = body.FirstColumnNumber
    last_col = body.LastColumnNumber
    mapping = FieldMappingService(schedule, schedule).build_mapping()
    field_infos = [match[mapping_side] for match in mapping["matches"]]
    best_row = body.FirstRowNumber
    best_match_count = -1
    for row in range(body.FirstRowNumber, body.LastRowNumber + 1):
        matched_heading_count = 0
        compared_count = 0
        for field_info in field_infos:
            col = first_col + field_info["field_index"]
            if col > last_col:
                continue
            compared_count += 1
            text = body.GetCellText(row, col)
            if text == field_info["heading"]:
                matched_heading_count += 1
        if matched_heading_count > best_match_count:
            best_match_count = matched_heading_count
            best_row = row
        if compared_count > 0 and matched_heading_count == compared_count:
            return row
    return best_row


def _body_title_style_pairs(source_schedule, target_schedule):
    mapping = FieldMappingService(source_schedule, target_schedule).build_mapping()
    source_body = get_section(source_schedule, "Body")
    target_body = get_section(target_schedule, "Body")
    source_row = _find_column_title_row(source_schedule, "source")
    target_row = _find_column_title_row(target_schedule, "target")
    pairs = []
    for match in mapping["matches"]:
        source_col = source_body.FirstColumnNumber + match["source"]["field_index"]
        target_col = target_body.FirstColumnNumber + match["target"]["field_index"]
        source_style = source_body.GetTableCellStyle(source_row, source_col)
        target_style = target_body.GetTableCellStyle(target_row, target_col)
        pairs.append(
            (
                {
                    "text": source_body.GetCellText(source_row, source_col),
                    "style": serialize_fields(source_schedule)[match["source"]["field_index"]]["style"],
                    "body_style_bold": source_style.IsFontBold,
                    "body_style_bg": None if source_style.BackgroundColor is None else (
                        source_style.BackgroundColor.Red,
                        source_style.BackgroundColor.Green,
                        source_style.BackgroundColor.Blue,
                    ),
                },
                {
                    "text": target_body.GetCellText(target_row, target_col),
                    "style": serialize_fields(target_schedule)[match["target"]["field_index"]]["style"],
                    "body_style_bold": target_style.IsFontBold,
                    "body_style_bg": None if target_style.BackgroundColor is None else (
                        target_style.BackgroundColor.Red,
                        target_style.BackgroundColor.Green,
                        target_style.BackgroundColor.Blue,
                    ),
                },
            )
        )
    return pairs


def test_column_title_copies_heading_text_for_matched_fields(revit_doc):
    case = HEADER_COPY_CASES[0]
    source = get_schedule(revit_doc, case["source_id"])
    target = get_schedule(revit_doc, case["target_id"])

    from Autodesk.Revit import DB

    tx = DB.Transaction(revit_doc, "pytest: custom copy column title")
    tx.Start()
    try:
        _ensure_show_headers_enabled(source, target)
        _mutate_target_title_texts(source, target)
        before_pairs = _mapped_column_title_pairs(source, target)
        result = CustomCopyService(revit_doc, source, target).apply({OPTION_COLUMN_TITLE})
        after_pairs = _mapped_column_title_pairs(source, target)
        tx.Commit()
    except Exception:
        tx.RollBack()
        raise

    assert OPTION_COLUMN_TITLE in result["applied"], result
    assert before_pairs != after_pairs
    for source_cell, target_cell in after_pairs:
        assert target_cell["column_heading"] == source_cell["column_heading"]


def test_column_grouping_skips_when_fewer_than_two_matched_columns(revit_doc):
    case = HEADER_COPY_CASES[0]
    source = get_schedule(revit_doc, case["source_id"])
    if not _has_source_grouping(source):
        pytest.skip("Source schedule does not expose a grouping span in this model.")

    from Autodesk.Revit import DB

    tx = DB.Transaction(revit_doc, "pytest: create reduced grouping target")
    tx.Start()
    try:
        target_id = source.Duplicate(DB.ViewDuplicateOption.Duplicate)
        target = revit_doc.GetElement(target_id)
        field_order = list(target.Definition.GetFieldOrder() or [])
        while len(field_order) > 1:
            target.Definition.RemoveField(field_order[-1])
            field_order = list(target.Definition.GetFieldOrder() or [])
        _ensure_show_headers_enabled(source, target)
        tx.Commit()
    except Exception:
        tx.RollBack()
        raise

    tx = DB.Transaction(revit_doc, "pytest: custom copy column grouping")
    tx.Start()
    try:
        result = CustomCopyService(revit_doc, source, target).apply({OPTION_COLUMN_GROUPING})
        tx.Commit()
    except Exception:
        tx.RollBack()
        raise

    assert not result["applied"], result
    assert result["skipped"], result


def test_column_grouping_recreates_group_when_two_or_more_columns_match(revit_doc):
    case = HEADER_COPY_CASES[0]
    source = get_schedule(revit_doc, case["source_id"])
    target = get_schedule(revit_doc, case["target_id"])
    if not _has_source_grouping(source):
        pytest.skip("Source schedule does not expose a grouping span in this model.")
    if not _has_two_or_more_mapped_group_columns(source, target):
        pytest.skip("Source grouping does not have two or more mapped target columns in this model.")

    from Autodesk.Revit import DB

    tx = DB.Transaction(revit_doc, "pytest: custom copy successful grouping")
    tx.Start()
    try:
        _ensure_show_headers_enabled(source, target)
        _clear_target_group_headers(target)
        before_spans = _body_group_spans(target)
        before_texts = _body_text_matrix(target)
        before_styles = _body_group_style_pairs(source, target)
        result = CustomCopyService(revit_doc, source, target).apply({OPTION_COLUMN_GROUPING})
        after_spans = _body_group_spans(target)
        after_texts = _body_text_matrix(target)
        after_styles = _body_group_style_pairs(source, target)
        tx.Commit()
    except Exception:
        tx.RollBack()
        raise

    assert OPTION_COLUMN_GROUPING in result["applied"], result
    assert before_spans != after_spans or before_texts != after_texts
    assert any(text not in (None, "") for row in after_texts for text in row), after_texts
    assert after_styles, after_styles
    for source_cell, target_cell in after_styles:
        assert target_cell["text"] == source_cell["text"]
        assert target_cell["is_bold"] == source_cell["is_bold"]
        assert target_cell["bg"] == source_cell["bg"]


def test_column_title_copies_style_for_matched_fields(revit_doc):
    case = HEADER_COPY_CASES[0]
    source = get_schedule(revit_doc, case["source_id"])
    target = get_schedule(revit_doc, case["target_id"])

    from Autodesk.Revit import DB

    tx = DB.Transaction(revit_doc, "pytest: custom copy column title style")
    tx.Start()
    try:
        _ensure_show_headers_enabled(source, target)
        _mutate_target_title_styles(source, target)
        before_body_pairs = _body_title_style_pairs(source, target)
        before_pairs = _mapped_column_title_pairs(source, target)
        result = CustomCopyService(revit_doc, source, target).apply({OPTION_COLUMN_TITLE})
        after_pairs = _mapped_column_title_pairs(source, target)
        after_body_pairs = _body_title_style_pairs(source, target)
        tx.Commit()
    except Exception:
        tx.RollBack()
        raise

    assert OPTION_COLUMN_TITLE in result["applied"], result
    assert before_body_pairs != after_body_pairs
    for source_cell, target_cell in after_body_pairs:
        assert target_cell["body_style_bold"] == source_cell["body_style_bold"]
        assert target_cell["body_style_bg"] == source_cell["body_style_bg"]


def test_column_title_skips_when_headers_are_hidden(revit_doc):
    case = HEADER_COPY_CASES[0]
    source = get_schedule(revit_doc, case["source_id"])
    target = get_schedule(revit_doc, case["target_id"])

    from Autodesk.Revit import DB

    tx = DB.Transaction(revit_doc, "pytest: custom copy hidden headers")
    tx.Start()
    try:
        source.Definition.ShowHeaders = False
        target.Definition.ShowHeaders = True
        result = CustomCopyService(revit_doc, source, target).apply({OPTION_COLUMN_TITLE})
        tx.Commit()
    except Exception:
        tx.RollBack()
        raise

    assert OPTION_COLUMN_TITLE not in result["applied"], result
    assert result["skipped"], result
