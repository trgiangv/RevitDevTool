"""Unit tests for suite lease state behavior."""

from __future__ import annotations

from pathlib import Path

from revitdevtool_pytest.discovery import RevitInstance
from revitdevtool_pytest.suite_leasing import SuiteLeaseStore


def _instance(pipe_name: str, version: int, process_id: int) -> RevitInstance:
    return RevitInstance(pipe_name=pipe_name, version=version, process_id=process_id)


def test_assign_and_resolve_existing(tmp_path: Path) -> None:
    state_file = tmp_path / "suite-leases.json"
    store = SuiteLeaseStore(state_file=state_file)
    suite_key = "suite-a"
    suite_path = str(tmp_path / "tests" / "conftest.py")
    instance = _instance("Revit_2025_1001", 2025, 1001)

    store.assign(suite_key, suite_path, instance)
    resolved = store.resolve_existing(suite_key, suite_path, [instance])

    assert resolved == instance


def test_find_free_excludes_other_suite_leases(tmp_path: Path) -> None:
    state_file = tmp_path / "suite-leases.json"
    store = SuiteLeaseStore(state_file=state_file)
    suite_a = "suite-a"
    suite_b = "suite-b"
    suite_path = str(tmp_path / "tests" / "conftest.py")
    leased = _instance("Revit_2025_1200", 2025, 1200)
    free = _instance("Revit_2025_1300", 2025, 1300)

    store.assign(suite_a, suite_path, leased)
    candidates = store.find_free(suite_b, [leased, free])

    assert candidates == [free]


def test_resolve_existing_prunes_stale_pid(tmp_path: Path) -> None:
    state_file = tmp_path / "suite-leases.json"
    store = SuiteLeaseStore(state_file=state_file)
    suite_key = "suite-a"
    suite_path = str(tmp_path / "tests" / "conftest.py")
    stale = _instance("Revit_2025_1400", 2025, 1400)
    current = _instance("Revit_2025_1500", 2025, 1500)

    store.assign(suite_key, suite_path, stale)
    resolved = store.resolve_existing(suite_key, suite_path, [current])

    assert resolved is None
    assert store.find_free("suite-b", [current]) == [current]
