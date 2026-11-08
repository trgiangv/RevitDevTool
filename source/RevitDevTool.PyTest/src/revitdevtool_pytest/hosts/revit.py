"""Revit installs via the Autodesk Revit registry key."""

from __future__ import annotations

import os
import winreg
from pathlib import Path
from typing import Final

from ..constants import REVIT
from .version import is_autodesk_version
from .winreg import open_hklm

_HOST: Final = REVIT
_EXE: Final = "Revit.exe"
_REGISTRY_KEY: Final = r"SOFTWARE\Autodesk\Revit\Autodesk Revit {version}"
_REGISTRY_VALUE: Final = "InstallationLocation"
_FOLDER: Final = "Revit {version}"


def supports(host_name: str) -> bool:
    return host_name == _HOST


def find_revit_executable(version: str) -> str | None:
    if not is_autodesk_version(version):
        return None
    path = _from_registry(version)
    if path:
        return path
    return _from_filesystem(version)


def _from_registry(version: str) -> str | None:
    try:
        with open_hklm(_REGISTRY_KEY.format(version=version)) as key:
            install_dir, _ = winreg.QueryValueEx(key, _REGISTRY_VALUE)
        if not isinstance(install_dir, str):
            return None
        exe = os.path.join(install_dir, _EXE)
        return exe if os.path.isfile(exe) else None
    except OSError:
        return None


def _from_filesystem(version: str) -> str | None:
    program_files = os.environ.get("ProgramFiles", r"C:\Program Files")
    autodesk_dir = os.path.join(program_files, "Autodesk")
    folder = _FOLDER.format(version=version)
    exact = os.path.join(autodesk_dir, folder, _EXE)
    if os.path.isfile(exact):
        return exact
    if not os.path.isdir(autodesk_dir):
        return None
    for entry in sorted(Path(autodesk_dir).glob(f"{folder}*")):
        exe = entry / _EXE
        if exe.is_file():
            return str(exe)
    return None
