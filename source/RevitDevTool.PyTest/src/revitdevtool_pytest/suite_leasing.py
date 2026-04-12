"""Suite-to-Revit instance lease state management."""

from __future__ import annotations

import json
import os
import random
import time
from dataclasses import dataclass
from pathlib import Path

from .discovery import RevitInstance

_STATE_DIR = Path.home() / ".revitdevtool_pytest"
_STATE_FILE = _STATE_DIR / "suite-leases.json"
_STATE_VERSION = 1
_SAVE_RETRY_DELAYS_S = (0.02, 0.05, 0.1, 0.2)


@dataclass(frozen=True, slots=True)
class SuiteLease:
    """Persistent lease binding a suite key to one Revit instance."""

    suite_key: str
    suite_path: str
    pipe_name: str
    process_id: int
    assigned_at: float
    last_seen_at: float

    def to_dict(self) -> dict[str, object]:
        return {
            "suite_key": self.suite_key,
            "suite_path": self.suite_path,
            "pipe_name": self.pipe_name,
            "process_id": self.process_id,
            "assigned_at": self.assigned_at,
            "last_seen_at": self.last_seen_at,
        }

    @classmethod
    def from_dict(cls, data: dict[str, object]) -> SuiteLease:
        return cls(
            suite_key=str(data.get("suite_key", "")),
            suite_path=str(data.get("suite_path", "")),
            pipe_name=str(data.get("pipe_name", "")),
            process_id=int(data.get("process_id", 0)),
            assigned_at=float(data.get("assigned_at", 0.0)),
            last_seen_at=float(data.get("last_seen_at", 0.0)),
        )


class SuiteLeaseStore:
    """Load/save suite lease map and perform allocation lookups."""

    def __init__(self, state_file: Path | None = None) -> None:
        self._state_file = state_file or _STATE_FILE
        self._leases = self._load_leases()

    def resolve_existing(
        self,
        suite_key: str,
        suite_path: str,
        instances: list[RevitInstance],
    ) -> RevitInstance | None:
        active_by_pid = {instance.process_id: instance for instance in instances}
        self._prune_stale(active_by_pid)

        lease = self._leases.get(suite_key)
        if lease is None:
            return None

        active = active_by_pid.get(lease.process_id)
        if active is None:
            self._leases.pop(suite_key, None)
            self._save_leases()
            return None

        self._leases[suite_key] = SuiteLease(
            suite_key=suite_key,
            suite_path=suite_path,
            pipe_name=active.pipe_name,
            process_id=active.process_id,
            assigned_at=lease.assigned_at,
            last_seen_at=time.time(),
        )
        self._save_leases()
        return active

    def find_free(
        self,
        suite_key: str,
        instances: list[RevitInstance],
    ) -> list[RevitInstance]:
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
        suite_path: str,
        instance: RevitInstance,
    ) -> None:
        now = time.time()
        existing = self._leases.get(suite_key)
        assigned_at = existing.assigned_at if existing is not None else now
        self._leases[suite_key] = SuiteLease(
            suite_key=suite_key,
            suite_path=suite_path,
            pipe_name=instance.pipe_name,
            process_id=instance.process_id,
            assigned_at=assigned_at,
            last_seen_at=now,
        )
        self._save_leases()

    def get_suite_process_id(self, suite_key: str) -> int | None:
        lease = self._leases.get(suite_key)
        return None if lease is None else lease.process_id

    def get_suite_lease(self, suite_key: str) -> SuiteLease | None:
        return self._leases.get(suite_key)

    def clear_suite(self, suite_key: str) -> None:
        if suite_key not in self._leases:
            return
        self._leases.pop(suite_key, None)
        self._save_leases()

    def _prune_stale(self, active_by_pid: dict[int, RevitInstance]) -> None:
        stale = [
            key
            for key, lease in self._leases.items()
            if lease.process_id not in active_by_pid
        ]
        if not stale:
            return
        for key in stale:
            self._leases.pop(key, None)
        self._save_leases()

    def _load_leases(self) -> dict[str, SuiteLease]:
        if not self._state_file.is_file():
            return {}
        try:
            payload = json.loads(self._state_file.read_text(encoding="utf-8"))
        except Exception:
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
        base_tmp = self._state_file.with_suffix(".tmp")

        for attempt, delay in enumerate((*_SAVE_RETRY_DELAYS_S, None), start=1):
            tmp_file = base_tmp.with_name(f"{base_tmp.stem}.{os.getpid()}.{random.randint(1000, 9999)}{base_tmp.suffix}")
            try:
                tmp_file.write_text(content, encoding="utf-8")
                os.replace(tmp_file, self._state_file)
                return
            except PermissionError:
                try:
                    tmp_file.unlink(missing_ok=True)
                except Exception:
                    pass
                if delay is None:
                    raise
                time.sleep(delay)
