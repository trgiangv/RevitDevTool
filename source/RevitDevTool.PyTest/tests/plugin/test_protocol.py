"""Pytest protocol check against ``instance/info`` (no host)."""

from __future__ import annotations

import pytest

from revitdevtool_pytest import __version__, connection
from revitdevtool_pytest.constants import PROTOCOL_CURRENT, PROTOCOL_MIN
from revitdevtool_pytest.models import RunRequest
from revitdevtool_pytest.protocol import (
    UPDATE_CLIENT_MESSAGE,
    UPDATE_PRODUCT_MESSAGE,
    explain_mismatch,
)


def _info(current: int, minimum: int, product: str = "5.0.0") -> dict:
    return {
        "hostApp": "Revit",
        "processId": 1,
        "versionNumber": "2025",
        "productVersion": product,
        "protocols": {"pytest": {"current": current, "min": minimum}},
    }


def test_host_without_protocols_says_to_update_revitdevtool():
    message = explain_mismatch(
        {"hostApp": "Revit", "processId": 1, "versionNumber": "2025"}, "1.0.0"
    )

    assert (
        message
        == f"RevitDevTool is not compatible with revitdevtool_pytest 1.0.0. {UPDATE_PRODUCT_MESSAGE}"
    )


def test_host_inside_the_range_is_accepted():
    assert explain_mismatch(_info(PROTOCOL_CURRENT, PROTOCOL_MIN)) is None
    assert explain_mismatch(_info(PROTOCOL_CURRENT + 1, PROTOCOL_MIN)) is None


def test_newer_host_says_to_update_the_package():
    message = explain_mismatch(
        _info(PROTOCOL_CURRENT + 2, PROTOCOL_CURRENT + 1), "0.5.0"
    )

    assert message is not None
    assert message.startswith(
        "RevitDevTool 5.0.0 is not compatible with revitdevtool_pytest 0.5.0"
    )
    assert message.endswith(UPDATE_CLIENT_MESSAGE)


def test_older_host_says_to_update_revitdevtool():
    message = explain_mismatch(
        _info(PROTOCOL_MIN - 1, PROTOCOL_MIN - 1, "4.0.0"), "1.0.0"
    )

    assert message is not None
    assert message.startswith(
        "RevitDevTool 4.0.0 is not compatible with revitdevtool_pytest 1.0.0"
    )
    assert message.endswith(UPDATE_PRODUCT_MESSAGE)


def test_run_request_carries_the_sender():
    params = RunRequest(test_root="C:/t", nodeids=["a.py::t"]).to_params()

    assert params["sender"] == {
        "version": __version__,
        "protocol": {"current": PROTOCOL_CURRENT, "min": PROTOCOL_MIN},
    }


class _Bridge:
    def __init__(self, info: dict):
        self._info = info
        self.disconnected = False

    def instance_info(self) -> dict:
        return self._info

    def disconnect(self) -> None:
        self.disconnected = True


def test_incompatible_host_ends_the_session_with_the_message():
    bridge = _Bridge(_info(PROTOCOL_CURRENT + 2, PROTOCOL_CURRENT + 1))

    with pytest.raises(pytest.exit.Exception) as exited:
        connection._exit_if_incompatible(bridge)

    assert bridge.disconnected
    assert UPDATE_CLIENT_MESSAGE in str(exited.value)
