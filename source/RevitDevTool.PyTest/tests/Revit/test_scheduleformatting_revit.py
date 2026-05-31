"""Revit API tests for template-oriented schedule formatting behavior."""

import pytest

from tests.schedule.constants import FIELD_REQUIRED_OPTIONS
from tests.schedule.constants import FORMATTING_REQUIRED_OPTIONS
from tests.schedule.constants import OPTION_APPEARANCE
from tests.schedule.constants import OPTION_FIELDS
from tests.schedule.constants import OPTION_FILTER
from tests.schedule.constants import OPTION_FORMATTING
from tests.schedule.constants import OPTION_HEADER
from tests.schedule.constants import OPTION_PHASE_FILTER
from tests.schedule.constants import OPTION_SORTING_GROUPING
from tests.schedule.constants import TEMPLATE_OPTIONS
from tests.schedule.template_workflow import exercise_temp_template_workflow
from tests.schedule.template_workflow import selected_option_flags
from tests.schedule.template_workflow import slugify_option_name
from tests.schedule.template_workflow import template_copy_options


pytestmark = pytest.mark.usefixtures("revit_auto_rollback")


def _effective_selected_names(selected_names):
    effective_names = set(selected_names)
    if OPTION_FIELDS not in effective_names:
        for option_name in FIELD_REQUIRED_OPTIONS:
            effective_names.discard(option_name)
    if OPTION_FORMATTING not in effective_names:
        for option_name in FORMATTING_REQUIRED_OPTIONS:
            effective_names.discard(option_name)
    return effective_names


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
    effective_names = _effective_selected_names(
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
def test_schedule_template_single_option_workflow(
    revit_doc, option_name, selected_names
):
    from tests.schedule.constants import SOURCE_SCHEDULE_ID
    from tests.schedule.constants import TARGET_SCHEDULE_ID

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


def test_schedule_basic_api(source_schedule, target_schedule, revit_doc):
    from Autodesk.Revit import DB

    # source
    # source_definition = source_schedule.Definition # type: DB.ScheduleDefinition
    # for i in range(source_definition.GetFieldCount()):
    #     field = source_definition.GetField(i) # type: DB.ScheduleField
    #     header = field.ColumnHeading
    
    #     title_cell_style = field.GetStyle() # type: DB.TableCellStyle
    #     print(title_cell_style.BackgroundColor.Red)
    #     print(title_cell_style.BackgroundColor.Green)
    #     print(title_cell_style.BackgroundColor.Blue)
    #     param_id = field.ParameterId
    #     print(
    #         f"Source Field {i}: {header})"
    #     )
    table_data = target_schedule.GetTableData() # type: DB.TableData
    section = table_data.GetSectionData(sectionType=DB.SectionType.Body) # type: DB.SectionData
    text = section.GetCellText(0, 2)
    print(f"First cell text: {text}")

    # target
    # target_definition = target_schedule.Definition # type: DB.ScheduleDefinition
    # for i in range(target_definition.GetFieldCount()):
    #     field = target_definition.GetField(i) # type: DB.ScheduleField
    #     title_cell_style = field.GetStyle() # type: DB.TableCellStyle
    #     print(title_cell_style.BackgroundColor)
    #     param_id = field.ParameterId
    #     header = field.ColumnHeading
    #     print(
    #         f"Target Field {i}: {header}"
    #     )