import json
from dataclasses import asdict
from pathlib import Path
from typing import Any

from revitdevtool_pytest.constants import PROTOCOL_CURRENT, PROTOCOL_MIN
from revitdevtool_pytest.models import CaseResult, RunRequest, RunResponse
from revitdevtool_pytest.protocol import _host_range, explain_mismatch

GOLDEN = Path(__file__).parent


def _load(name: str) -> dict[str, Any]:
    return json.loads((GOLDEN / name).read_text(encoding="utf-8"))


def _wire(value: Any) -> Any:
    return json.loads(json.dumps(value))


def test_run_request_roundtrips():
    data = _load("run_request.json")

    assert _wire(RunRequest(**data).to_params()) == data


def test_run_response_roundtrips():
    data = _load("run_response.json")

    assert _wire(asdict(RunResponse.from_dict(data))) == data


def test_progress_roundtrips():
    data = _load("progress_notification.json")

    assert _wire(asdict(CaseResult.from_dict(data))) == data


def test_instance_info_reads_range():
    info = _load("instance_info.json")

    assert _host_range(info) == (PROTOCOL_CURRENT, PROTOCOL_MIN)
    assert info["productVersion"] == "4.0.0-beta.8"
    assert explain_mismatch(info) is None


def test_missing_engine_is_none():
    data = _load("run_response.json")
    del data["engine"]

    assert RunResponse.from_dict(data).engine is None
