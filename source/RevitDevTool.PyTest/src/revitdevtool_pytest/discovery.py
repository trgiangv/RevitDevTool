"""Discover running Revit instances and launch Revit if needed."""

from __future__ import annotations

import os
import re
import subprocess
import time
import winreg
from dataclasses import dataclass

from .constants import (
    DEFAULT_LAUNCH_TIMEOUT_S,
    DEFAULT_POLL_INTERVAL_S,
    PIPE_DIR,
    PIPE_PATTERN,
    REVIT_EXE,
    REVIT_NOSPLASH,
    REVIT_REGISTRY_KEY,
    REVIT_REGISTRY_VALUE,
)

_PIPE_RE = re.compile(PIPE_PATTERN)


@dataclass(frozen=True, slots=True)
class RevitInstance:
    pipe_name: str
    version: int
    process_id: int


def find_revit_pipes() -> list[RevitInstance]:
    """Scan Named Pipes for pipes matching ``Revit_{year}_{pid}``."""
    instances: list[RevitInstance] = []
    for name in _list_named_pipes():
        m = _PIPE_RE.match(name)
        if m:
            process_id = int(m.group(2))
            version = int(m.group(1))
            instances.append(RevitInstance(name, version, process_id))
    return instances


def select_instance(
    instances: list[RevitInstance],
    version: int | None = None,
) -> RevitInstance | None:
    """Find a running instance matching *version*, or the latest if unspecified.

    When *version* is given, only an exact match is returned — no fallback.
    """
    if not instances:
        return None
    if version is not None:
        matches = [i for i in instances if i.version == version]
        if not matches:
            return None
        # Deterministic selection for multiple instances of the same year:
        # prefer highest PID (typically newest launched process).
        return max(matches, key=lambda i: i.process_id)

    # Prefer newest version; tie-break by highest PID.
    return max(instances, key=lambda i: (i.version, i.process_id))


def find_revit_path(version: int) -> str | None:
    """Locate ``Revit.exe`` via registry, falling back to the default path."""
    path = _find_from_registry(version)
    if path:
        return path
    default = f"C:\\Program Files\\Autodesk\\Revit {version}\\Revit.exe"
    return default if os.path.isfile(default) else None


def launch_revit(
    version: int,
    wait_timeout_s: float = DEFAULT_LAUNCH_TIMEOUT_S,
) -> RevitInstance | None:
    """Start Revit with ``/nosplash`` and wait for its Named Pipe to appear.

    Returns the discovered ``RevitInstance``, or ``None`` on timeout.
    Raises ``FileNotFoundError`` if the requested version is not installed.
    """
    exe_path = find_revit_path(version)
    if exe_path is None:
        raise FileNotFoundError(f"Revit {version} installation not found.")

    subprocess.Popen(  # noqa: S603
        [exe_path, REVIT_NOSPLASH],
        creationflags=subprocess.DETACHED_PROCESS,
    )

    return wait_for_revit_pipe(version, timeout_s=wait_timeout_s)


def start_revit(version: int) -> int:
    """Start Revit with ``/nosplash`` and return the spawned process id."""
    exe_path = find_revit_path(version)
    if exe_path is None:
        raise FileNotFoundError(f"Revit {version} installation not found.")

    process = subprocess.Popen(  # noqa: S603
        [exe_path, REVIT_NOSPLASH],
        creationflags=subprocess.DETACHED_PROCESS,
    )
    return int(process.pid)


def wait_for_revit_pipe(
    version: int | None = None,
    timeout_s: float = DEFAULT_LAUNCH_TIMEOUT_S,
    poll_interval_s: float = DEFAULT_POLL_INTERVAL_S,
) -> RevitInstance | None:
    """Block until a Revit pipe matching *version* appears."""
    deadline = time.monotonic() + timeout_s
    while time.monotonic() < deadline:
        match = select_instance(find_revit_pipes(), version)
        if match is not None:
            return match
        time.sleep(poll_interval_s)
    return None


def _find_from_registry(version: int) -> str | None:
    try:
        with winreg.OpenKey(
            winreg.HKEY_LOCAL_MACHINE,
            REVIT_REGISTRY_KEY.format(version=version),
        ) as key:
            install_dir, _ = winreg.QueryValueEx(key, REVIT_REGISTRY_VALUE)
        exe = os.path.join(install_dir, REVIT_EXE)
        return exe if os.path.isfile(exe) else None
    except OSError:
        return None


def _list_named_pipes() -> list[str]:
    """Return base names of all Named Pipes visible to the current user."""
    try:
        return os.listdir(PIPE_DIR)
    except OSError:
        return []
