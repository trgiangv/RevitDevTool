"""Discover running host instances and launch hosts if needed."""

from __future__ import annotations

import os
import re
import subprocess
import time
import winreg
from dataclasses import dataclass
from pathlib import Path

from .constants import (
    _ACAD_EXE,
    ACAD_REGISTRY_ROOT,
    DEFAULT_LAUNCH_TIMEOUT_S,
    DEFAULT_POLL_INTERVAL_S,
    HOST_REGISTRY,
    PIPE_DIR,
    PIPE_PATTERN,
    HostConfig,
    get_host_config,
)

_PIPE_RE = re.compile(PIPE_PATTERN)
_ACAD_PRODUCT_KEY_RE = re.compile(r"ACAD-[0-9A-F]\d(?P<productId>\d{2})", re.IGNORECASE)

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
        instances.append(HostInstance(
            pipe_name=name,
            host_name=resolved,
            version=m.group(2),
            process_id=int(m.group(3)),
        ))
    return instances


def find_host_executable(host_name: str, version: str) -> str | None:
    """Locate the host executable via registry, falling back to filesystem.

    Returns None when the host has no registered exe discovery logic
    (e.g. Rhino, Tekla — connect via explicit pipe or auto-discovery).
    """
    cfg = get_host_config(host_name)
    if cfg.exe_name is None:
        return None
    if cfg.acad_product_ids is not None:
        return _find_acad_family_exe(cfg, version)
    return _find_generic_exe(cfg, version)


def start_host(host_name: str, version: str) -> int:
    """Start the host application and return the spawned process id."""
    cfg = get_host_config(host_name)
    exe_path = find_host_executable(host_name, version)
    if exe_path is None:
        raise FileNotFoundError(f"{host_name} {version} installation not found.")

    process = subprocess.Popen(  # noqa: S603
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


# ---------------------------------------------------------------------------
# Generic exe resolver (Revit + any host with registry_key/default_dir)
# ---------------------------------------------------------------------------

def _find_generic_exe(cfg: HostConfig, version: str) -> str | None:
    assert cfg.exe_name is not None
    if cfg.registry_key and cfg.registry_value:
        path = _exe_from_registry(cfg, version)
        if path:
            return path

    program_files = os.environ.get("ProgramFiles", r"C:\Program Files")
    autodesk_dir = os.path.join(program_files, "Autodesk")

    if cfg.default_dir_pattern:
        default = os.path.join(autodesk_dir, cfg.default_dir_pattern.format(version=version), cfg.exe_name)
        if os.path.isfile(default):
            return default

    if cfg.default_dir_pattern and os.path.isdir(autodesk_dir):
        pattern = f"{cfg.default_dir_pattern.format(version=version)}*"
        for entry in sorted(Path(autodesk_dir).glob(pattern)):
            exe = entry / cfg.exe_name
            if exe.is_file():
                return str(exe)

    return None


def _exe_from_registry(cfg: HostConfig, version: str) -> str | None:
    assert cfg.registry_key is not None and cfg.registry_value is not None and cfg.exe_name is not None
    try:
        with winreg.OpenKey(
            winreg.HKEY_LOCAL_MACHINE,
            cfg.registry_key.format(version=version),
        ) as key:
            install_dir, _ = winreg.QueryValueEx(key, cfg.registry_value)
        exe = os.path.join(install_dir, cfg.exe_name)
        return exe if os.path.isfile(exe) else None
    except OSError:
        return None


# ---------------------------------------------------------------------------
# AutoCAD-family registry resolver (mirrors AcadPathResolver.cs)
# ---------------------------------------------------------------------------

def _find_acad_family_exe(cfg: HostConfig, version: str) -> str | None:
    """Enumerate AutoCAD registry for a product matching *cfg.acad_product_ids* and *version*."""
    exe = _acad_from_registry(cfg, version)
    if exe:
        return exe
    return _acad_from_filesystem(version)


def _acad_from_registry(cfg: HostConfig, version: str) -> str | None:
    assert cfg.acad_product_ids is not None
    target_ids = {pid.lower() for pid in cfg.acad_product_ids}

    try:
        root = winreg.OpenKey(winreg.HKEY_LOCAL_MACHINE, ACAD_REGISTRY_ROOT)
    except OSError:
        return None

    try:
        for i in range(_enum_count(root)):
            release_name = winreg.EnumKey(root, i)
            try:
                release_key = winreg.OpenKey(root, release_name)
            except OSError:
                continue
            try:
                result = _scan_release_products(release_key, target_ids, version)
                if result is not None:
                    return result
            finally:
                winreg.CloseKey(release_key)
    finally:
        winreg.CloseKey(root)
    return None


def _scan_release_products(release_key: winreg.HKEYType, target_ids: set[str], version: str) -> str | None:
    for j in range(_enum_count(release_key)):
        product_key_name = winreg.EnumKey(release_key, j)
        m = _ACAD_PRODUCT_KEY_RE.search(product_key_name)
        if not m:
            continue
        product_id = m.group("productId").lower()
        if product_id not in target_ids:
            continue

        try:
            product_key = winreg.OpenKey(release_key, product_key_name)
        except OSError:
            continue
        try:
            year = _read_reg_str(product_key, "UPIRELEASE")
            if year != version:
                continue
            exe = _resolve_acad_exe(product_key)
            if exe:
                return exe
        finally:
            winreg.CloseKey(product_key)
    return None


def _resolve_acad_exe(product_key: winreg.HKEYType) -> str | None:
    for value_name, trim_trailing in [("GlobUPILocation", True), ("AcadLocation", False)]:
        location = _read_reg_str(product_key, value_name)
        if not location:
            continue
        if trim_trailing:
            parent = os.path.dirname(location.rstrip("\\/"))
            if parent:
                exe = os.path.join(parent, _ACAD_EXE)
                if os.path.isfile(exe):
                    return exe
        exe = os.path.join(location, _ACAD_EXE)
        if os.path.isfile(exe):
            return exe
    return None


def _acad_from_filesystem(version: str) -> str | None:
    program_files = os.environ.get("ProgramFiles", r"C:\Program Files")
    autodesk_dir = os.path.join(program_files, "Autodesk")
    if not os.path.isdir(autodesk_dir):
        return None

    patterns = [f"AutoCAD {version}", f"AutoCAD {version} *"]
    for pattern in patterns:
        for entry in sorted(Path(autodesk_dir).glob(pattern)):
            exe = entry / _ACAD_EXE
            if exe.is_file():
                return str(exe)
    return None


# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def _read_reg_str(key: winreg.HKEYType, name: str) -> str | None:
    try:
        val, _ = winreg.QueryValueEx(key, name)
        return val if isinstance(val, str) and val.strip() else None
    except OSError:
        return None


def _enum_count(key: winreg.HKEYType) -> int:
    try:
        _, count, _ = winreg.QueryInfoKey(key)
        return count
    except OSError:
        return 0


def _list_named_pipes() -> list[str]:
    """Return base names of all Named Pipes visible to the current user."""
    try:
        return os.listdir(PIPE_DIR)
    except OSError:
        return []
