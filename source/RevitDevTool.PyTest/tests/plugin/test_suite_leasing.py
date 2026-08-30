from pathlib import Path

from revitdevtool_pytest.discovery import HostInstance
from revitdevtool_pytest.suite_leasing import SuiteLeaseStore


def test_save_leases_uses_tempfile_not_random(tmp_path: Path):
    state = tmp_path / "suite-leases.json"
    store = SuiteLeaseStore(state_file=state)
    store.assign(
        "abc123",
        HostInstance(
            pipe_name="DevTools_Revit_2025_1",
            host_name="revit",
            version="2025",
            process_id=1,
        ),
    )
    assert state.is_file()
    reloaded = SuiteLeaseStore(state_file=state)
    lease = reloaded.get_suite_lease("abc123")
    assert lease is not None
    assert lease.process_id == 1
    leftovers = list(tmp_path.glob("*.tmp"))
    assert leftovers == []
