"""
Module reset test script for CodeExecute Python runtime.

How to read output across runs:
1) If reset works, `run_id_within_session` should be 1 every time you execute the script.
2) `session_id` and `imported_at` should change between separate script executions.
3) `cache_state` should start from zeroed cache counters on each new execution.
"""

from __future__ import annotations

import os
import sys
from pathlib import Path
from pprint import pprint

# Ensure project root is importable even when script root is `commands`.
project_root = Path(__file__).resolve().parent.parent
if str(project_root) not in sys.path:
    sys.path.append(str(project_root))

from test_module import run_reset_diagnostics


def main() -> None:
    print("=== Module Reset Diagnostic ===")
    print(f"PID: {os.getpid()}")
    print(f"Script: {__file__}")
    print()

    result = run_reset_diagnostics()
    pprint(str(result))

    print()
    print("Expected if reset is correct:")
    print("- run_id_within_session = 1 for each click/run")
    print("- session_id changes between separate runs")
    print("- cache_state starts with misses from fresh state")


if __name__ == "__main__":
    main()
