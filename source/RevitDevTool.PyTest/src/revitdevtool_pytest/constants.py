"""Centralized constants for RevitDevTool.PyTest."""

PLUGIN_NAME = "RevitDevTool.PyTest"

BRIDGE_METHOD_TESTS_EXECUTE = "tests/execute"
BRIDGE_MSG_TYPE_NOTIFICATION = "notification"

OPT_VERSION = "revit_version"
OPT_TIMEOUT = "revit_timeout"
OPT_PIPE = "revit_pipe"
OPT_LAUNCH = "revit_launch"
OPT_LAUNCH_TIMEOUT = "revit_launch_timeout"

DEFAULT_TEST_TIMEOUT_S = 60.0
DEFAULT_LAUNCH_TIMEOUT_S = 120.0
DEFAULT_CONNECT_TIMEOUT_MS = 30_000
DEFAULT_POLL_INTERVAL_S = 2.0

PIPE_DIR = r"//./pipe"
PIPE_PATTERN = r"^Revit_(\d{4})_(\d+)$"

REVIT_EXE = "Revit.exe"
REVIT_NOSPLASH = "/nosplash"
REVIT_DEFAULT_DIR = r"C:\Program Files\Autodesk"
REVIT_REGISTRY_KEY = r"SOFTWARE\Autodesk\Revit\Autodesk Revit {version}"
REVIT_REGISTRY_VALUE = "InstallationLocation"

EXIT_CODE_CONFIG_ERROR = 4
