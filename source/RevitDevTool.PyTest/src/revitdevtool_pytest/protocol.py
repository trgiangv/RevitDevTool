"""Protocol range check against the host, right after connecting.

The host reports its range in ``instance/info`` under ``protocols.pytest``;
a host without it is too old to talk to this package.
"""

from __future__ import annotations

from typing import Any

from . import __version__
from .constants import (
    PROTOCOL_CURRENT,
    PROTOCOL_MIN,
    PROTOCOL_NAME,
)

UPDATE_CLIENT_MESSAGE = "Update revitdevtool_pytest to the latest version."
UPDATE_PRODUCT_MESSAGE = "Update RevitDevTool to the latest version."

SENDER: dict[str, Any] = {
    "version": __version__,
    "protocol": {"current": PROTOCOL_CURRENT, "min": PROTOCOL_MIN},
}


def explain_mismatch(
    info: dict[str, Any], client_version: str = __version__
) -> str | None:
    """The message to show when this package and the host are not compatible, else ``None``."""
    host_current, host_min = _host_range(info)
    if PROTOCOL_MIN <= host_current and host_min <= PROTOCOL_CURRENT:
        return None

    host = f"RevitDevTool {info.get('productVersion', '')}".rstrip()
    message = (
        UPDATE_PRODUCT_MESSAGE if host_current < PROTOCOL_MIN else UPDATE_CLIENT_MESSAGE
    )
    return (
        f"{host} is not compatible with revitdevtool_pytest {client_version}. {message}"
    )


def _host_range(info: dict[str, Any]) -> tuple[int, int]:
    entry = info.get("protocols", {}).get(PROTOCOL_NAME, {"current": 0, "min": 0})
    return int(entry["current"]), int(entry["min"])
