import pytest

from tests.schedule.constants import HEADER_COPY_CASES
from tests.schedule.field_mapping import FieldMappingService
from tests.schedule.model import get_schedule


pytestmark = pytest.mark.usefixtures("revit_auto_rollback")


def test_field_mapping_prefers_parameter_id(revit_doc):
    case = HEADER_COPY_CASES[0]
    source = get_schedule(revit_doc, case["source_id"])
    target = get_schedule(revit_doc, case["target_id"])

    mapping = FieldMappingService(source, target).build_mapping()

    assert mapping["matches"], mapping
    for match in mapping["matches"]:
        assert match["source"]["parameter_id"] is not None, match
        assert match["source"]["parameter_id"] == match["target"]["parameter_id"], match


def test_field_mapping_maps_each_target_column_once(revit_doc):
    case = HEADER_COPY_CASES[0]
    source = get_schedule(revit_doc, case["source_id"])
    target = get_schedule(revit_doc, case["target_id"])

    mapping = FieldMappingService(source, target).build_mapping()
    target_indexes = [match["target"]["field_index"] for match in mapping["matches"]]

    assert len(target_indexes) == len(set(target_indexes)), mapping
