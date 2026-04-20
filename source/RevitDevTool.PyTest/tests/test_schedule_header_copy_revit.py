import pytest

from tests.schedule.constants import HEADER_COPY_CASES
from tests.schedule.contracts import HeaderContractInspector
from tests.schedule.field_mapping import FieldMappingService
from tests.schedule.header_workflow import copy_header_title_rows
from tests.schedule.model import get_schedule
from tests.schedule.model import get_section


pytestmark = pytest.mark.usefixtures("revit_auto_rollback")


def _body_column_count(schedule):
    body = get_section(schedule, "Body")
    return body.LastColumnNumber - body.FirstColumnNumber + 1


def _header_text_by_field_index(schedule):
    inspector = HeaderContractInspector(schedule)
    contract = inspector.build_contract()
    row_cells = inspector.visual_cells(contract["row_count"])[-1]
    return [cell["text"] for cell in row_cells]


def _mapped_target_texts(source_schedule, target_schedule):
    source_texts = _header_text_by_field_index(source_schedule)
    target_texts = _header_text_by_field_index(target_schedule)
    mapping = FieldMappingService(source_schedule, target_schedule).build_mapping()

    pairs = []
    for match in mapping["matches"]:
        source_index = match["source"]["field_index"]
        target_index = match["target"]["field_index"]
        if source_index >= len(source_texts) or target_index >= len(target_texts):
            continue
        pairs.append((source_texts[source_index], target_texts[target_index]))
    return pairs


def test_header_copy_allows_different_body_column_counts(revit_doc):
    from Autodesk.Revit import DB

    case = HEADER_COPY_CASES[0]
    source = get_schedule(revit_doc, case["source_id"])

    tx = DB.Transaction(revit_doc, "pytest: create reduced target schedule")
    tx.Start()
    try:
        target_id = source.Duplicate(DB.ViewDuplicateOption.Duplicate)
        target = revit_doc.GetElement(target_id)
        field_order = list(target.Definition.GetFieldOrder() or [])
        if len(field_order) >= 2:
            target.Definition.RemoveField(field_order[-1])
        tx.Commit()
    except Exception:
        tx.RollBack()
        raise

    assert _body_column_count(source) != _body_column_count(target)

    tx = DB.Transaction(revit_doc, "pytest: copy header across field mismatch")
    tx.Start()
    try:
        copied = copy_header_title_rows(revit_doc, source, target)
        tx.Commit()
    except Exception:
        tx.RollBack()
        raise

    assert copied is True


def test_header_copy_only_updates_matched_columns(revit_doc):
    case = HEADER_COPY_CASES[0]
    source = get_schedule(revit_doc, case["source_id"])
    target = get_schedule(revit_doc, case["target_id"])
    before_pairs = _mapped_target_texts(source, target)

    from Autodesk.Revit import DB

    tx = DB.Transaction(revit_doc, "pytest: copy matched header columns")
    tx.Start()
    try:
        copied = copy_header_title_rows(revit_doc, source, target)
        tx.Commit()
    except Exception:
        tx.RollBack()
        raise

    after_pairs = _mapped_target_texts(source, target)

    assert copied is True
    assert before_pairs != after_pairs
    for source_text, target_text in after_pairs:
        assert target_text == source_text
