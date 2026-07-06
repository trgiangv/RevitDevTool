"""Centralized constants for RevitDevTool.PyTest."""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Final

PLUGIN_NAME: Final = "RevitDevTool.PyTest"

BRIDGE_METHOD_TESTS_RUN: Final = "tests/run"
BRIDGE_MSG_TYPE_NOTIFICATION: Final = "notification"
BRIDGE_NOTIFY_TEST_PROGRESS: Final = "notifications/tests/progress"

OPT_HOST: Final = "host_name"
OPT_VERSION: Final = "host_version"
OPT_TIMEOUT: Final = "host_timeout"
OPT_PIPE: Final = "host_pipe"
OPT_LAUNCH: Final = "host_launch"
OPT_LAUNCH_TIMEOUT: Final = "host_launch_timeout"

DEFAULT_HOST: Final = "revit"
DEFAULT_TEST_TIMEOUT_S: Final = 60.0
DEFAULT_LAUNCH_TIMEOUT_S: Final = 120.0
DEFAULT_CONNECT_TIMEOUT_MS: Final = 30_000
DEFAULT_POLL_INTERVAL_S: Final = 2.0

PIPE_DIR: Final = r"//./pipe"

PIPE_PREFIX: Final = "DevTools"

# Mirrors C# InstanceManager: DevTools_{Host}_{Version}_{PID}
# Version is [^_]+ (any non-underscore chars) to support year, semver, etc.
PIPE_PATTERN: Final = rf"^{PIPE_PREFIX}_(\w+)_([^_]+)_(\d+)$"

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


@dataclass(frozen=True, slots=True)
class HostConfig:
    """Executable discovery and launch config for one host product.

    Not all fields are required. Hosts without ``exe_name`` can only be
    reached via ``--host-pipe`` (explicit pipe) or auto-discovery of
    already-running instances.
    """

    pipe_prefix: str
    exe_name: str | None = None
    launch_args: list[str] = field(default_factory=list)
    registry_key: str | None = None
    registry_value: str | None = None
    default_dir_pattern: str | None = None
    acad_product_ids: list[str] | None = None


ACAD_REGISTRY_ROOT: Final = r"SOFTWARE\Autodesk\AutoCAD"
_ACAD_EXE: Final = "acad.exe"
_ACAD_DIR: Final = "AutoCAD {version}"
_ACAD_ARGS: Final = ["/nologo"]

HOST_REGISTRY: dict[str, HostConfig] = {
    # --- Autodesk Revit ---
    "revit": HostConfig(
        pipe_prefix="Revit",
        exe_name="Revit.exe",
        registry_key=r"SOFTWARE\Autodesk\Revit\Autodesk Revit {version}",
        registry_value="InstallationLocation",
        default_dir_pattern="Revit {version}",
        launch_args=["/nosplash"],
    ),
    # --- Autodesk AutoCAD family ---
    "autocad": HostConfig(
        pipe_prefix="AutoCad",
        exe_name=_ACAD_EXE,
        default_dir_pattern=_ACAD_DIR,
        launch_args=_ACAD_ARGS,
        acad_product_ids=["01"],
    ),
    "civil3d": HostConfig(
        pipe_prefix="Civil3D",
        exe_name=_ACAD_EXE,
        default_dir_pattern=_ACAD_DIR,
        launch_args=_ACAD_ARGS,
        acad_product_ids=["00"],
    ),
    "plant3d": HostConfig(
        pipe_prefix="Plant3D",
        exe_name=_ACAD_EXE,
        default_dir_pattern=_ACAD_DIR,
        launch_args=_ACAD_ARGS,
        acad_product_ids=["17"],
    ),
    "acadarch": HostConfig(
        pipe_prefix="AcadArch",
        exe_name=_ACAD_EXE,
        default_dir_pattern=_ACAD_DIR,
        launch_args=_ACAD_ARGS,
        acad_product_ids=["04"],
    ),
    "acadmech": HostConfig(
        pipe_prefix="AcadMech",
        exe_name=_ACAD_EXE,
        default_dir_pattern=_ACAD_DIR,
        launch_args=_ACAD_ARGS,
        acad_product_ids=["05"],
    ),
    "acadmep": HostConfig(
        pipe_prefix="AcadMep",
        exe_name=_ACAD_EXE,
        default_dir_pattern=_ACAD_DIR,
        launch_args=_ACAD_ARGS,
        acad_product_ids=["06"],
    ),
    "acadelec": HostConfig(
        pipe_prefix="AcadElec",
        exe_name=_ACAD_EXE,
        default_dir_pattern=_ACAD_DIR,
        launch_args=_ACAD_ARGS,
        acad_product_ids=["07"],
    ),
    "acadmap3d": HostConfig(
        pipe_prefix="AcadMap3D",
        exe_name=_ACAD_EXE,
        default_dir_pattern=_ACAD_DIR,
        launch_args=_ACAD_ARGS,
        acad_product_ids=["02"],
    ),
    # --- Autodesk Navisworks ---
    "navisworks": HostConfig(
        pipe_prefix="Navisworks",
    ),
    # --- Non-Autodesk hosts ---
    "rhino": HostConfig(
        pipe_prefix="Rhino",
    ),
    "tekla": HostConfig(
        pipe_prefix="Tekla",
    ),
}


def get_host_config(host_name: str) -> HostConfig:
    """Look up a HostConfig by normalized name.

    Returns a minimal config with pipe_prefix = host_name if the host
    is not in the registry. This allows connecting to any host that
    exposes a DevToolsPipeServer pipe without pre-registration.
    """
    key = host_name.strip().lower()
    config = HOST_REGISTRY.get(key)
    if config is not None:
        return config
    return HostConfig(pipe_prefix=host_name)


def is_acad_family(host_name: str) -> bool:
    """Return True if *host_name* is an AutoCAD-family product."""
    cfg = HOST_REGISTRY.get(host_name.strip().lower())
    return cfg is not None and cfg.acad_product_ids is not None
