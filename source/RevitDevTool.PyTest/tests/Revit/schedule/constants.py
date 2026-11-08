"""Shared constants for schedule formatting Revit tests."""

SOURCE_SCHEDULE_ID = 1662251  # Air Terminal Schedule 2
TARGET_SCHEDULE_ID = 1661028  # Air Terminal Schedule

OPTION_PHASE_FILTER = "Phase Filter"
OPTION_FIELDS = "Fields"
OPTION_FILTER = "Filter"
OPTION_SORTING_GROUPING = "Sorting/Grouping"
OPTION_FORMATTING = "Formatting"
OPTION_HEADER = "Header"
OPTION_APPEARANCE = "Appearance"

TEMPLATE_OPTIONS = (
    OPTION_PHASE_FILTER,
    OPTION_FIELDS,
    OPTION_FILTER,
    OPTION_SORTING_GROUPING,
    OPTION_FORMATTING,
    OPTION_APPEARANCE,
)

FIELD_REQUIRED_OPTIONS = (
    OPTION_FILTER,
    OPTION_SORTING_GROUPING,
    OPTION_FORMATTING,
)

FORMATTING_REQUIRED_OPTIONS = (
    OPTION_HEADER,
)

HEADER_COPY_CASES = (
    {
        "name": "complex_to_simple",
        "source_id": SOURCE_SCHEDULE_ID,
        "target_id": TARGET_SCHEDULE_ID,
    },
    {
        "name": "simple_to_complex",
        "source_id": TARGET_SCHEDULE_ID,
        "target_id": SOURCE_SCHEDULE_ID,
    },
)

HEADER_COPY_OPTION_SET = {
    OPTION_FIELDS,
    OPTION_FORMATTING,
    OPTION_HEADER,
}


def get_option_builtin_parameters():
    from Autodesk.Revit import DB

    return {
        OPTION_PHASE_FILTER: DB.BuiltInParameter.VIEW_PHASE_FILTER,
        OPTION_FIELDS: DB.BuiltInParameter.SCHEDULE_FIELDS_PARAM,
        OPTION_FILTER: DB.BuiltInParameter.SCHEDULE_FILTER_PARAM,
        OPTION_SORTING_GROUPING: DB.BuiltInParameter.SCHEDULE_GROUP_PARAM,
        OPTION_FORMATTING: DB.BuiltInParameter.SCHEDULE_FORMAT_PARAM,
        OPTION_APPEARANCE: DB.BuiltInParameter.SCHEDULE_SHEET_APPEARANCE_PARAM,
    }