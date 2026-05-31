"""Test-side header copy workflow using Revit API only."""

from tests.schedule.field_mapping import FieldMappingService


def get_header_section(schedule):
    from tests.schedule.model import get_section

    return get_section(schedule, "Header")


def get_body_section(schedule):
    from tests.schedule.model import get_section

    return get_section(schedule, "Body")


def copy_header_title_rows(doc, source_schedule, target_schedule):
    source_header = get_header_section(source_schedule)
    target_header = get_header_section(target_schedule)
    source_body = get_body_section(source_schedule)
    target_body = get_body_section(target_schedule)
    mapping = FieldMappingService(source_schedule, target_schedule).build_mapping()
    if not mapping["matches"]:
        return False

    source_title_row = source_header.LastRowNumber
    target_title_row = target_header.LastRowNumber
    copied_anything = False
    for match in mapping["matches"]:
        source_col = source_header.FirstColumnNumber + match["source"]["field_index"]
        target_col = target_header.FirstColumnNumber + match["target"]["field_index"]
        if source_col > source_body.LastColumnNumber:
            continue
        if target_col > target_body.LastColumnNumber:
            continue
        try:
            text = source_header.GetCellText(source_title_row, source_col)
            target_header.SetCellText(target_title_row, target_col, text)
            copied_anything = True
        except Exception:
            pass
    return copied_anything
