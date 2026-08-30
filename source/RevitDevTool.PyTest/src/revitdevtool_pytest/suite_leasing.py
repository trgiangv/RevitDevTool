"""Suite-to-host instance lease state management."""

from __future__ import annotations

import json
import os
import tempfile
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from .discovery import HostInstance

_STATE_DIR = Path.home() / ".revitdevtool_pytest"
_STATE_FILE = _STATE_DIR / "suite-leases.json"
_STATE_VERSION = 3
_SAVE_RETRY_DELAYS_S = (0.02, 0.05, 0.1, 0.2)


@dataclass(frozen=True, slots=True)
class SuiteLease:
    """Persistent lease binding a suite key to one host instance."""

    suite_key: str
    pipe_name: str
    process_id: int

    def to_dict(self) -> dict[str, Any]:
        return {
            "suite_key": self.suite_key,
            "pipe_name": self.pipe_name,
            "process_id": self.process_id,
        }

    @classmethod
    def from_dict(cls, data: dict[str, Any]) -> SuiteLease:
        return cls(
            suite_key=str(data.get("suite_key", "")),
            pipe_name=str(data.get("pipe_name", "")),
            process_id=int(data.get("process_id", 0)),
        )


class SuiteLeaseStore:
    """Load/save suite lease map and perform allocation lookups."""

    def __init__(self, state_file: Path | None = None) -> None:
        self._state_file = state_file or _STATE_FILE
        self._leases = self._load_leases()

    def find_free(
        self,
        suite_key: str,
        instances: list[HostInstance],
    ) -> list[HostInstance]:
        occupied = {
            lease.process_id
            for key, lease in self._leases.items()
            if key != suite_key
        }
        free = [instance for instance in instances if instance.process_id not in occupied]
        return sorted(free, key=lambda item: (item.version, item.process_id), reverse=True)

    def assign(
        self,
        suite_key: str,
        instance: HostInstance,
    ) -> None:
        self._leases[suite_key] = SuiteLease(
            suite_key=suite_key,
            pipe_name=instance.pipe_name,
            process_id=instance.process_id,
        )
        self._save_leases()

    def get_suite_lease(self, suite_key: str) -> SuiteLease | None:
        return self._leases.get(suite_key)

    def clear_suite(self, suite_key: str) -> None:
        if suite_key not in self._leases:
            return
        self._leases.pop(suite_key, None)
        self._save_leases()

    def _load_leases(self) -> dict[str, SuiteLease]:
        if not self._state_file.is_file():
            return {}
        try:
            payload = json.loads(self._state_file.read_text(encoding="utf-8"))
        except Exception: # noqa
            return {}

        if not isinstance(payload, dict):
            return {}
        if int(payload.get("version", 0)) != _STATE_VERSION:
            return {}

        raw = payload.get("suite_leases")
        if not isinstance(raw, dict):
            return {}

        leases: dict[str, SuiteLease] = {}
        for suite_key, value in raw.items():
            if not isinstance(suite_key, str) or not isinstance(value, dict):
                continue
            lease = SuiteLease.from_dict(value)
            if lease.suite_key:
                leases[suite_key] = lease
        return leases

    def _save_leases(self) -> None:
        self._state_file.parent.mkdir(parents=True, exist_ok=True)
        payload = {
            "version": _STATE_VERSION,
            "suite_leases": {key: lease.to_dict() for key, lease in self._leases.items()},
        }
        content = json.dumps(payload, ensure_ascii=False, indent=2)
        encoded = content.encode("utf-8")
        delays: tuple[float | None, ...] = (*_SAVE_RETRY_DELAYS_S, None)
        for delay in delays:
            try:
                _atomic_write(self._state_file, encoded)
                return
            except PermissionError:
                if delay is None:
                    raise
                time.sleep(delay)


def _atomic_write(path: Path, encoded: bytes) -> None:
    fd, tmp_name = tempfile.mkstemp(
        prefix=f"{path.stem}.",
        suffix=".tmp",
        dir=str(path.parent),
    )
    tmp_file = Path(tmp_name)
    try:
        os.write(fd, encoded)
        os.close(fd)
        fd = -1
        os.replace(tmp_file, path)
    except BaseException:
        if fd >= 0:
            try:
                os.close(fd)
            except OSError:
                pass
        try:
            tmp_file.unlink(missing_ok=True)
        except Exception:  # noqa
            pass
        raise
