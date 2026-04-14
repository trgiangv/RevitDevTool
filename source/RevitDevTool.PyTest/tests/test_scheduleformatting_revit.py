"""Revit tests for schedule formatting package."""

import pytest

from tests.schedule.constants import FIELD_REQUIRED_OPTIONS
from tests.schedule.constants import FORMATTING_REQUIRED_OPTIONS
from tests.schedule.constants import HEADER_COPY_CASES
from tests.schedule.constants import OPTION_APPEARANCE
from tests.schedule.constants import OPTION_FIELDS
from tests.schedule.constants import OPTION_FILTER
from tests.schedule.constants import OPTION_FORMATTING
from tests.schedule.constants import OPTION_HEADER
from tests.schedule.constants import OPTION_PHASE_FILTER
from tests.schedule.constants import OPTION_SORTING_GROUPING
from tests.schedule.constants import SOURCE_SCHEDULE_ID
from tests.schedule.constants import TARGET_SCHEDULE_ID
from tests.schedule.constants import TEMPLATE_OPTIONS
from tests.schedule.contracts import HeaderContractInspector
from tests.schedule.copy_workflow import effective_selected_names
from tests.schedule.copy_workflow import ScheduleCopyScenarioRunner
from tests.schedule.copy_workflow import run_header_copy_case
from tests.schedule.model import get_schedule
from tests.schedule.model import get_section
from tests.schedule.template_workflow import configure_temp_template
from tests.schedule.template_workflow import exercise_temp_template_workflow
from tests.schedule.template_workflow import selected_option_flags
from tests.schedule.template_workflow import slugify_option_name
from tests.schedule.template_workflow import template_copy_options


pytestmark = pytest.mark.usefixtures("revit_auto_rollback")


def _find_schedule_name_row(schedule):
    inspector = HeaderContractInspector(schedule)
    schedule_name = schedule.Name.lower()
    first_col = inspector.header.FirstColumnNumber
    last_col = inspector.header.LastColumnNumber
    for row in range(inspector.header.FirstRowNumber, inspector.header.LastRowNumber + 1):
        row_spans = inspector.row_spans(row)
        for span in row_spans:
            text = inspector.header.GetCellText(row, span["left"])
            if not text:
                continue
            normalized_text = text.strip().lower()
            if schedule_name in normalized_text:
                return row
            if normalized_text.startswith("<") and normalized_text.endswith(">"):
                return row
        if inspector.nonempty_visual_cell_count(row) <= 1:
            for span in row_spans:
                if span["left"] == first_col and span["right"] == last_col:
                    return row
    return None


def _remove_row(section, row):
    remove_row = getattr(section, "RemoveRow", None)
    if remove_row is not None:
        remove_row(row)
        return

    delete_row = getattr(section, "DeleteRow", None)
    if delete_row is not None:
        delete_row(row)
        return

    raise AssertionError("Header section row removal is not supported in this runtime")


def _mutate_source_insert_top_row(source):
    from Autodesk.Revit import DB

    header = get_section(source, "Header")
    first_row = header.FirstRowNumber
    first_col = header.FirstColumnNumber
    last_col = header.LastColumnNumber

    header.InsertRow(first_row)
    header.SetMergedCell(
        first_row,
        first_col,
        DB.TableMergedCell(first_row, first_col, first_row, last_col),
    )
    header.SetCellText(first_row, first_col, "added top row")


def _mutate_source_delete_schedule_name_row(source):
    row = _find_schedule_name_row(source)
    assert row is not None, "Could not find schedule-name row to delete"
    header = get_section(source, "Header")
    _remove_row(header, row)


def _run_mutated_header_copy(revit_doc, mode, mutator):
    from Autodesk.Revit import DB

    source = get_schedule(revit_doc, SOURCE_SCHEDULE_ID)
    target = get_schedule(revit_doc, TARGET_SCHEDULE_ID)
    tx = DB.Transaction(revit_doc, "pytest: mutate source schedule")
    tx.Start()
    try:
        mutator(source)
        tx.Commit()
    except Exception:
        tx.RollBack()
        raise

    runner = ScheduleCopyScenarioRunner(revit_doc, None, source_schedule=source, target_schedule=target)
    return runner.run(
        mode,
        FIELD_REQUIRED_OPTIONS,
        FORMATTING_REQUIRED_OPTIONS,
        configure_temp_template,
    )


def _run_header_copy_use_case(revit_doc, mode, case=None, mutator=None):
    if mutator is not None:
        return _run_mutated_header_copy(revit_doc, mode, mutator)

    assert case is not None, "Either case or mutator must be provided"
    return run_header_copy_case(
        revit_doc,
        case,
        mode,
        FIELD_REQUIRED_OPTIONS,
        FORMATTING_REQUIRED_OPTIONS,
        configure_temp_template,
    )


def test_schedule_template_option_constants_are_complete():
    assert TEMPLATE_OPTIONS == (
        OPTION_PHASE_FILTER,
        OPTION_FIELDS,
        OPTION_FILTER,
        OPTION_SORTING_GROUPING,
        OPTION_FORMATTING,
        OPTION_APPEARANCE,
    )

    assert FIELD_REQUIRED_OPTIONS == (
        OPTION_FILTER,
        OPTION_SORTING_GROUPING,
        OPTION_FORMATTING,
    )

    assert FORMATTING_REQUIRED_OPTIONS == (OPTION_HEADER,)


def test_schedule_template_field_dependents_turn_off_without_fields():
    selected_options = template_copy_options(
        controls=[],
        user_selection={OPTION_PHASE_FILTER, OPTION_APPEARANCE, OPTION_FILTER},
    )

    assert selected_option_flags(selected_options) == {
        OPTION_PHASE_FILTER: True,
        OPTION_FIELDS: False,
        OPTION_FILTER: False,
        OPTION_SORTING_GROUPING: False,
        OPTION_FORMATTING: False,
        OPTION_APPEARANCE: True,
    }


def test_schedule_header_child_turns_off_without_formatting():
    effective_names = effective_selected_names(
        {OPTION_FIELDS, OPTION_HEADER, OPTION_APPEARANCE}
    )

    assert effective_names == {OPTION_FIELDS, OPTION_APPEARANCE}


@pytest.mark.parametrize(
    ("option_name", "selected_names"),
    [
        (OPTION_PHASE_FILTER, {OPTION_PHASE_FILTER}),
        (OPTION_FIELDS, {OPTION_FIELDS}),
        (OPTION_FILTER, {OPTION_FIELDS, OPTION_FILTER}),
        (OPTION_SORTING_GROUPING, {OPTION_FIELDS, OPTION_SORTING_GROUPING}),
        (OPTION_FORMATTING, {OPTION_FIELDS, OPTION_FORMATTING}),
        (OPTION_APPEARANCE, {OPTION_APPEARANCE}),
    ],
)
def test_schedule_template_single_option_workflow(revit_doc, option_name, selected_names):
    result = exercise_temp_template_workflow(
        revit_doc,
        selected_names=selected_names,
        template_name="abc2_%s" % slugify_option_name(option_name),
        source_schedule_id=SOURCE_SCHEDULE_ID,
        target_schedule_id=TARGET_SCHEDULE_ID,
    )

    missing = [
        name
        for name, option in result["selected_options"].items()
        if option["control"] is None
    ]
    assert not missing, {
        "missing": missing,
        "available": sorted(control["name"] for control in result["controls"]),
    }

    option_control = result["selected_options"][option_name]["control"]
    assert option_control["include"] is True

    expected_selection = {
        name: name in selected_names for name in TEMPLATE_OPTIONS
    }
    if OPTION_FIELDS not in selected_names:
        for dependent_name in FIELD_REQUIRED_OPTIONS:
            expected_selection[dependent_name] = False

    assert selected_option_flags(result["selected_options"]) == expected_selection
    assert result["target_template_id"] == result["created_template_id"]
    assert result["removed_template_id"] == -1
    assert result["template_deleted"] is True


@pytest.mark.parametrize(
    ("mode", "case", "mutator"),
    [
        (mode, case, None)
        for mode in ("direct", "apply")
        for case in HEADER_COPY_CASES
    ]
    + [
        (mode, None, _mutate_source_insert_top_row)
        for mode in ("direct", "apply")
    ]
    + [
        (mode, None, _mutate_source_delete_schedule_name_row)
        for mode in ("direct", "apply")
    ],
    ids=[
        "%s_%s" % (mode, case["name"])
        for mode in ("direct", "apply")
        for case in HEADER_COPY_CASES
    ]
    + [
        "%s_inserted_top_row" % mode
        for mode in ("direct", "apply")
    ]
    + [
        "%s_deleted_schedule_name_row" % mode
        for mode in ("direct", "apply")
    ],
)
def test_schedule_header_copy_matches_source_contract(revit_doc, mode, case, mutator):
    debug_data = _run_header_copy_use_case(
        revit_doc,
        mode,
        case=case,
        mutator=mutator,
    )

    assert debug_data["row_count"] >= 1, debug_data
    assert debug_data["matches_expected"] is True, debug_data