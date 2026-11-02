"""Centralized constants for RevitDevTool.PyTest."""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Final

PLUGIN_NAME: Final = "RevitDevTool.PyTest"

BRIDGE_METHOD_TESTS_RUN: Final = "tests/run"
BRIDGE_METHOD_IPY_TESTS_RUN: Final = "ipytests/run"
BRIDGE_MSG_TYPE_NOTIFICATION: Final = "notification"
BRIDGE_NOTIFY_TEST_PROGRESS: Final = "notifications/tests/progress"

OPT_HOST: Final = "host_name"
OPT_VERSION: Final = "host_version"
OPT_PER_TEST_TIMEOUT: Final = "per_test_timeout"
OPT_PIPE: Final = "host_pipe"
OPT_FORCE_LAUNCH: Final = "force_launch"
OPT_LAUNCH_TIMEOUT: Final = "launch_timeout"

# Lowercase form of the RevitDevTool HostApp enum
REVIT: Final = "revit"  # HostApp.Revit
AUTOCAD: Final = "autocad"  # HostApp.AutoCad
CIVIL3D: Final = "civil3d"  # HostApp.Civil3D
PLANT3D: Final = "plant3d"  # HostApp.Plant3D
ACAD_ARCH: Final = "acadarch"  # HostApp.AcadArch
ACAD_MECH: Final = "acadmech"  # HostApp.AcadMech
ACAD_ELEC: Final = "acadelec"  # HostApp.AcadElec
ACAD_MEP: Final = "acadmep"  # HostApp.AcadMep
ACAD_MAP3D: Final = "acadmap3d"  # HostApp.AcadMap3D
NAVISWORKS: Final = "navisworks"  # HostApp.Navisworks
RHINO: Final = "rhino"  # HostApp.Rhino
TEKLA: Final = "tekla"  # HostApp.Tekla

DEFAULT_HOST: Final = REVIT
DEFAULT_TEST_TIMEOUT_S: Final = 60.0
DEFAULT_LAUNCH_TIMEOUT_S: Final = 120.0
DEFAULT_CONNECT_TIMEOUT_MS: Final = 30_000
DEFAULT_POLL_INTERVAL_S: Final = 2.0

PIPE_DIR: Final = r"//./pipe"

PIPE_PREFIX: Final = "DevTools"

# Host pipe name: DevTools_{Host}_{Version}_{PID}
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

NODEID_SEP: Final = "::"
SUITE_ITEM_NAME: Final = "(suite)"

IPY_TEST_PREFIX: Final = "test_"
IPY_TEST_SUFFIX: Final = "_ipy.py"


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


_ACAD_EXE: Final = "acad.exe"
_ACAD_ARGS: Final = ["/nologo"]

HOST_REGISTRY: dict[str, HostConfig] = {
    REVIT: HostConfig(
        pipe_prefix="Revit",
        exe_name="Revit.exe",
        launch_args=["/nosplash"],
    ),
    AUTOCAD: HostConfig(
        pipe_prefix="AutoCad",
        exe_name=_ACAD_EXE,
        launch_args=_ACAD_ARGS,
    ),
    CIVIL3D: HostConfig(
        pipe_prefix="Civil3D",
        exe_name=_ACAD_EXE,
        launch_args=_ACAD_ARGS,
    ),
    PLANT3D: HostConfig(
        pipe_prefix="Plant3D",
        exe_name=_ACAD_EXE,
        launch_args=_ACAD_ARGS,
    ),
    ACAD_ARCH: HostConfig(
        pipe_prefix="AcadArch",
        exe_name=_ACAD_EXE,
        launch_args=_ACAD_ARGS,
    ),
    ACAD_MECH: HostConfig(
        pipe_prefix="AcadMech",
        exe_name=_ACAD_EXE,
        launch_args=_ACAD_ARGS,
    ),
    ACAD_ELEC: HostConfig(
        pipe_prefix="AcadElec",
        exe_name=_ACAD_EXE,
        launch_args=_ACAD_ARGS,
    ),
    ACAD_MEP: HostConfig(
        pipe_prefix="AcadMep",
        exe_name=_ACAD_EXE,
        launch_args=_ACAD_ARGS,
    ),
    ACAD_MAP3D: HostConfig(
        pipe_prefix="AcadMap3D",
        exe_name=_ACAD_EXE,
        launch_args=_ACAD_ARGS,
    ),
    NAVISWORKS: HostConfig(
        pipe_prefix="Navisworks",
    ),
    RHINO: HostConfig(
        pipe_prefix="Rhino",
    ),
    TEKLA: HostConfig(
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
