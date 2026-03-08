from __future__ import annotations

import json
from pathlib import Path


_DEFAULT_PROTOCOL_VERSION = "1.0"
_DEFAULT_SCHEMA_VERSION = "2026-03-08"
_DEFAULT_SOURCE_CHECKSUM = "20260308b2"
_MIRROR_PATH = Path(__file__).resolve().parents[1] / "schema_mirror.json"


def _load_schema_mirror() -> dict[str, str]:
    try:
        return json.loads(_MIRROR_PATH.read_text(encoding="utf-8"))
    except Exception:
        return {}


_mirror = _load_schema_mirror()

PROTOCOL_VERSION = str(_mirror.get("protocolVersion", _DEFAULT_PROTOCOL_VERSION))
SCHEMA_VERSION = str(_mirror.get("schemaVersion", _DEFAULT_SCHEMA_VERSION))
SOURCE_CHECKSUM = str(_mirror.get("sourceChecksum", _DEFAULT_SOURCE_CHECKSUM))
