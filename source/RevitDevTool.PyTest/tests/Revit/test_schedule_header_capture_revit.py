import os

import pytest

from schedule.constants import SOURCE_SCHEDULE_ID
from schedule.constants import TARGET_SCHEDULE_ID
from schedule.model import get_schedule
from schedule.serializer import SNAPSHOT_FILE
from schedule.serializer import schedule_snapshot_key
from schedule.serializer import serialize_schedule_list
from schedule.serializer import write_snapshot_file


pytestmark = pytest.mark.usefixtures("revit_auto_rollback")


def test_export_schedule_snapshots_to_test_json(revit_doc):
    source = get_schedule(revit_doc, SOURCE_SCHEDULE_ID)
    target = get_schedule(revit_doc, TARGET_SCHEDULE_ID)

    payload = serialize_schedule_list([source, target])
    write_snapshot_file(payload)

    assert os.path.isfile(SNAPSHOT_FILE)
    assert schedule_snapshot_key(source) in payload
    assert schedule_snapshot_key(target) in payload
    assert payload[schedule_snapshot_key(source)]["sections"]
    assert payload[schedule_snapshot_key(target)]["sections"]
