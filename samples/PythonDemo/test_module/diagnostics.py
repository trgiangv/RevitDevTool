import sys
from typing import Any, Dict

from test_module import state
from test_module.engine import cache_info, run_pipeline


def run_reset_diagnostics() -> Dict[str, Any]:
    first = run_pipeline(seed=7, extra_values=(13.0, 21.0))
    second = run_pipeline(seed=7, extra_values=(34.0,))

    loaded = sorted(name for name in sys.modules if name.startswith("test_module"))

    return {
        "run_id_within_session": state.next_run_id(),
        "session_id": state.SESSION_ID,
        "imported_at": state.IMPORTED_AT,
        "cache_state": cache_info(),
        "first_packet": {"name": first.name, "values": first.values},
        "second_packet": {"name": second.name, "values": second.values},
        "loaded_module_count": len(loaded),
        "loaded_modules": loaded,
    }
