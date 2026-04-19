"""Centralized constants for RevitDevTool.PyTest."""

from typing import Final

PLUGIN_NAME: Final = "RevitDevTool.PyTest"

BRIDGE_METHOD_TESTS_DISCOVER: Final = "tests/discover"
BRIDGE_METHOD_TESTS_RUN: Final = "tests/run"
BRIDGE_MSG_TYPE_NOTIFICATION: Final = "notification"
BRIDGE_NOTIFY_TEST_PROGRESS: Final = "notifications/tests/progress"

OPT_VERSION: Final = "revit_version"
OPT_TIMEOUT: Final = "revit_timeout"
OPT_PIPE: Final = "revit_pipe"
OPT_LAUNCH: Final = "revit_launch"
OPT_LAUNCH_TIMEOUT: Final = "revit_launch_timeout"

DEFAULT_TEST_TIMEOUT_S: Final = 60.0
DEFAULT_LAUNCH_TIMEOUT_S: Final = 120.0
DEFAULT_CONNECT_TIMEOUT_MS: Final = 30_000
DEFAULT_POLL_INTERVAL_S: Final = 2.0

PIPE_DIR: Final = r"//./pipe"
PIPE_PATTERN: Final = r"^Revit_(\d{4})_(\d+)$"

REVIT_EXE: Final = "Revit.exe"
REVIT_NOSPLASH: Final = "/nosplash"
REVIT_DEFAULT_DIR: Final = r"C:\Program Files\Autodesk"
REVIT_REGISTRY_KEY: Final = r"SOFTWARE\Autodesk\Revit\Autodesk Revit {version}"
REVIT_REGISTRY_VALUE: Final = "InstallationLocation"

EXIT_CODE_CONFIG_ERROR: Final = 4

OUTCOME_PASSED: Final = "passed"
OUTCOME_FAILED: Final = "failed"
OUTCOME_SKIPPED: Final = "skipped"
OUTCOME_ERROR: Final = "error"
OUTCOME_XFAILED: Final = "xfailed"
OUTCOME_XPASSED: Final = "xpassed"

PHASE_SETUP: Final = "setup"
PHASE_CALL: Final = "call"
PHASE_TEARDOWN: Final = "teardown"
