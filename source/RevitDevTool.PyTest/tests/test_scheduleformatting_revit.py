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
    for row in range(
        inspector.header.FirstRowNumber, inspector.header.LastRowNumber + 1
    ):
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
    target = _duplicate_source_as_compatible_target(revit_doc, source)
    tx = DB.Transaction(revit_doc, "pytest: mutate source schedule")
    tx.Start()
    try:
        mutator(source)
        tx.Commit()
    except Exception:
        tx.RollBack()
        raise

    runner = ScheduleCopyScenarioRunner(
        revit_doc, None, source_schedule=source, target_schedule=target
    )
    return runner.run(
        mode,
        FIELD_REQUIRED_OPTIONS,
        FORMATTING_REQUIRED_OPTIONS,
        configure_temp_template,
    )


def _duplicate_source_as_compatible_target(revit_doc, source_schedule):
    from Autodesk.Revit import DB

    tx = DB.Transaction(revit_doc, "pytest: duplicate compatible target schedule")
    tx.Start()
    try:
        target_id = source_schedule.Duplicate(DB.ViewDuplicateOption.Duplicate)
        target = revit_doc.GetElement(target_id)
        header = get_section(target, "Header")
        first_row = header.FirstRowNumber

        while header.LastRowNumber > first_row:
            _remove_row(header, header.LastRowNumber)

        for col in range(header.FirstColumnNumber, header.LastColumnNumber + 1):
            header.SetMergedCell(
                first_row,
                col,
                DB.TableMergedCell(first_row, col, first_row, col),
            )
            header.SetCellText(first_row, col, "")

        tx.Commit()
        return target
    except Exception:
        tx.RollBack()
        raise


def _run_compatible_header_copy(revit_doc, mode, mutator=None):
    from Autodesk.Revit import DB

    source = get_schedule(revit_doc, SOURCE_SCHEDULE_ID)
    target = _duplicate_source_as_compatible_target(revit_doc, source)

    if mutator is not None:
        tx = DB.Transaction(revit_doc, "pytest: mutate source schedule")
        tx.Start()
        try:
            mutator(source)
            tx.Commit()
        except Exception:
            tx.RollBack()
            raise

    runner = ScheduleCopyScenarioRunner(
        revit_doc, None, source_schedule=source, target_schedule=target
    )
    return runner.run(
        mode,
        FIELD_REQUIRED_OPTIONS,
        FORMATTING_REQUIRED_OPTIONS,
        configure_temp_template,
    )


def _set_target_schedule_title_row(target, text):
    from Autodesk.Revit import DB

    header = get_section(target, "Header")
    first_row = header.FirstRowNumber
    first_col = header.FirstColumnNumber
    last_col = header.LastColumnNumber
    header.SetMergedCell(
        first_row,
        first_col,
        DB.TableMergedCell(first_row, first_col, first_row, last_col),
    )
    header.SetCellText(first_row, first_col, text)


def _run_compatible_header_copy_with_target_setup(revit_doc, mode, target_mutator):
    from Autodesk.Revit import DB

    source = get_schedule(revit_doc, SOURCE_SCHEDULE_ID)
    target = _duplicate_source_as_compatible_target(revit_doc, source)

    tx = DB.Transaction(revit_doc, "pytest: mutate target schedule")
    tx.Start()
    try:
        target_mutator(target)
        tx.Commit()
    except Exception:
        tx.RollBack()
        raise

    runner = ScheduleCopyScenarioRunner(
        revit_doc, None, source_schedule=source, target_schedule=target
    )
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


def _header_column_count(schedule):
    header = get_section(schedule, "Header")
    return header.LastColumnNumber - header.FirstColumnNumber + 1


def _body_column_count(schedule):
    body = get_section(schedule, "Body")
    return body.LastColumnNumber - body.FirstColumnNumber + 1


def _selected_source_target(revit_doc, case=None):
    if case is None:
        return (
            get_schedule(revit_doc, SOURCE_SCHEDULE_ID),
            get_schedule(revit_doc, TARGET_SCHEDULE_ID),
        )
    return (
        get_schedule(revit_doc, case["source_id"]),
        get_schedule(revit_doc, case["target_id"]),
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


def test_schedule_header_copy_option_is_off_by_default_in_xaml():
    xaml_path = r"C:\Users\Giang.VuTruong\workspace\automation-development-copy-schedule-formatting\pyRevit\AureconGEN.extension\AureconGEN.tab\Documentation.panel\Stack3.stack\Schedule Link.pulldown\ScheduleFormatting.pushbutton\schedule_formatting_view.xaml"
    with open(xaml_path, "r") as stream:
        xaml_text = stream.read()

    assert 'x:Name="chkHeader"' in xaml_text
    assert 'Content="Header"' in xaml_text
    assert 'IsChecked="False"' in xaml_text


def test_schedule_header_copy_uses_visual_body_column_compatibility(revit_doc):
    source, target = _selected_source_target(revit_doc, HEADER_COPY_CASES[0])

    assert _body_column_count(source) == 3
    assert _body_column_count(target) == 3
    assert _header_column_count(source) != _header_column_count(target)

    debug_data = _run_header_copy_use_case(
        revit_doc,
        "direct",
        case=HEADER_COPY_CASES[0],
    )

    assert debug_data["result"]["applied"] == [target.Name]
    assert debug_data["matches_expected"] is True, debug_data


def test_schedule_header_copy_preserves_special_target_title_row(revit_doc):
    debug_data = _run_header_copy_use_case(
        revit_doc,
        "direct",
        case=HEADER_COPY_CASES[0],
    )

    target_rows = debug_data["target_contract"]["visual_cells"]

    assert len(target_rows) == 3, debug_data
    assert len(target_rows[0]) == 1, debug_data
    assert target_rows[0][0]["left"] == 0, debug_data
    assert target_rows[0][0]["right"] == 2, debug_data
    assert target_rows[1][1]["text"] == "nnn", debug_data
    assert target_rows[2][0]["text"] == "aa", debug_data
    assert target_rows[2][1]["text"] == "aa", debug_data


def test_schedule_header_copy_replicates_full_source_header_layout(revit_doc):
    debug_data = _run_header_copy_use_case(
        revit_doc,
        "direct",
        case=HEADER_COPY_CASES[0],
    )

    source_rows = debug_data["source_contract"]["visual_cells"]
    target_rows = debug_data["target_contract"]["visual_cells"]

    assert len(source_rows) == 3, debug_data
    assert len(target_rows) == 3, debug_data
    assert source_rows[0][1]["text"] == "nnn"
    assert len(source_rows[2]) == 2
    assert source_rows[2][0]["text"] == "aa"
    assert source_rows[2][1]["text"] == "aa"
    assert target_rows == source_rows, debug_data


def test_schedule_header_copy_handles_existing_target_title_row(revit_doc):
    debug_data = _run_compatible_header_copy_with_target_setup(
        revit_doc,
        "direct",
        lambda target: _set_target_schedule_title_row(
            target, "<Air Terminal Schedule>"
        ),
    )

    source_contract = debug_data["source_contract"]["visual_cells"]
    target_contract = debug_data["target_contract"]["visual_cells"]

    assert len(target_contract) == len(source_contract), debug_data
    assert target_contract[2][0]["text"] == "aa", debug_data
    assert target_contract[2][1]["text"] == "aa", debug_data


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
def test_schedule_template_single_option_workflow(
    revit_doc, option_name, selected_names
):
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

    expected_selection = {name: name in selected_names for name in TEMPLATE_OPTIONS}
    if OPTION_FIELDS not in selected_names:
        for dependent_name in FIELD_REQUIRED_OPTIONS:
            expected_selection[dependent_name] = False

    assert selected_option_flags(result["selected_options"]) == expected_selection
    assert result["target_template_id"] == result["created_template_id"]
    assert result["removed_template_id"] == -1
    assert result["template_deleted"] is True


@pytest.mark.parametrize(
    ("mode", "mutator"),
    [(mode, None) for mode in ("direct", "apply")]
    + [(mode, _mutate_source_insert_top_row) for mode in ("direct", "apply")]
    + [(mode, _mutate_source_delete_schedule_name_row) for mode in ("direct", "apply")],
    ids=["%s_baseline" % mode for mode in ("direct", "apply")]
    + ["%s_inserted_top_row" % mode for mode in ("direct", "apply")]
    + ["%s_deleted_schedule_name_row" % mode for mode in ("direct", "apply")],
)
def test_schedule_header_copy_matches_source_contract(revit_doc, mode, mutator):
    debug_data = _run_compatible_header_copy(revit_doc, mode, mutator=mutator)

    assert debug_data["row_count"] >= 1, debug_data
    assert debug_data["matches_expected"] is True, debug_data
