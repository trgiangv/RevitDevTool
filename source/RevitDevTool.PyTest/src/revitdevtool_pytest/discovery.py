"""Discover running host instances and launch hosts if needed.

Executable lookup is delegated to ``hosts``. This module owns pipes and process start.
"""

from __future__ import annotations

import os
import re
import subprocess
import time
from dataclasses import dataclass

from .constants import (
    DEFAULT_LAUNCH_TIMEOUT_S,
    DEFAULT_POLL_INTERVAL_S,
    HOST_REGISTRY,
    PIPE_DIR,
    PIPE_PATTERN,
    get_host_config,
)
from .hosts import find_executable

_PIPE_RE = re.compile(PIPE_PATTERN)

_PREFIX_TO_HOST: dict[str, str] = {
    cfg.pipe_prefix.lower(): name for name, cfg in HOST_REGISTRY.items()
}


@dataclass(frozen=True, slots=True)
class HostInstance:
    pipe_name: str
    host_name: str
    version: str
    process_id: int


def find_host_pipes(host_name: str | None = None) -> list[HostInstance]:
    """Scan Named Pipes for pipes matching ``{Host}_{Version}_{PID}``.

    When *host_name* is given, only pipes whose prefix matches the
    host's ``pipe_prefix`` are returned.
    """
    target_prefix: str | None = None
    if host_name is not None:
        cfg = get_host_config(host_name)
        target_prefix = cfg.pipe_prefix.lower()

    instances: list[HostInstance] = []
    for name in _list_named_pipes():
        m = _PIPE_RE.match(name)
        if not m:
            continue
        prefix_raw = m.group(1)
        prefix_lower = prefix_raw.lower()
        if target_prefix is not None and prefix_lower != target_prefix:
            continue
        resolved = _PREFIX_TO_HOST.get(prefix_lower, prefix_raw)
        instances.append(
            HostInstance(
                pipe_name=name,
                host_name=resolved,
                version=m.group(2),
                process_id=int(m.group(3)),
            )
        )
    return instances


def start_host(host_name: str, version: str) -> int:
    """Start the host application and return the spawned process id."""
    cfg = get_host_config(host_name)
    exe_path = find_executable(host_name, version)
    if exe_path is None:
        raise FileNotFoundError(f"{host_name} {version} installation not found.")

    process = subprocess.Popen(
        [exe_path, *cfg.launch_args],
        creationflags=subprocess.DETACHED_PROCESS,
    )
    return int(process.pid)


def wait_for_host_pipe(
    host_name: str,
    version: str | None = None,
    timeout_s: float = DEFAULT_LAUNCH_TIMEOUT_S,
    poll_interval_s: float = DEFAULT_POLL_INTERVAL_S,
    process_id: int | None = None,
) -> HostInstance | None:
    """Block until a host pipe appears.

    When *process_id* is given, wait for that exact process to register its pipe.
    """
    deadline = time.monotonic() + timeout_s
    while time.monotonic() < deadline:
        instances = find_host_pipes(host_name)
        if process_id is not None:
            match = next((i for i in instances if i.process_id == process_id), None)
        elif not instances:
            match = None
        elif version is not None:
            matches = [i for i in instances if i.version == version]
            match = max(matches, key=lambda i: i.process_id) if matches else None
        else:
            match = max(instances, key=lambda i: (i.version, i.process_id))
        if match is not None:
            return match
        time.sleep(poll_interval_s)
    return None


def _list_named_pipes() -> list[str]:
    """Return base names of all Named Pipes visible to the current user."""
    try:
        return os.listdir(PIPE_DIR)
    except OSError:
        return []
