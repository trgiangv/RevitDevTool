"""AutoCAD-family installs via InstalledProducts."""

from __future__ import annotations

import os
import winreg
from pathlib import Path
from typing import Final

from ..constants import (
    ACAD_ARCH,
    ACAD_ELEC,
    ACAD_MAP3D,
    ACAD_MECH,
    ACAD_MEP,
    AUTOCAD,
    CIVIL3D,
    PLANT3D,
    HostConfig,
)
from .version import is_autodesk_version, version_from_install_dir
from .winreg import open_hklm, subkey_count

ACAD_REGISTRY_ROOT: Final = r"SOFTWARE\Autodesk\AutoCAD"
ACAD_INSTALLED_PRODUCTS: Final = "InstalledProducts"

# InstalledProducts subkey, keyed by the host name constants.
_PRODUCT_CODES: Final[dict[str, str]] = {
    AUTOCAD: "ACAD",
    CIVIL3D: "C3D",
    PLANT3D: "PLNT3D",
    ACAD_MAP3D: "MAP",
    ACAD_ARCH: "ACA",
    ACAD_MECH: "ACADM",
    ACAD_MEP: "MEP",
    ACAD_ELEC: "ACADE",
}


def product_code(host_name: str) -> str | None:
    """Return the InstalledProducts subkey for *host_name*."""
    return _PRODUCT_CODES.get(host_name)


def find_acad_executable(host_name: str, cfg: HostConfig, version: str) -> str | None:
    """Locate acad.exe for *host_name* at *version*.

    Verticals resolve only through InstalledProducts. Vanilla AutoCAD may
    fall back to ``AutoCAD {version}`` on disk.
    """
    code = product_code(host_name)
    if code is None or not is_autodesk_version(version):
        return None
    exe = _from_registry(code, cfg, version)
    if exe:
        return exe
    if host_name != AUTOCAD:
        return None
    return _from_filesystem(cfg, version)


def _from_registry(code: str, cfg: HostConfig, version: str) -> str | None:
    try:
        root = open_hklm(ACAD_REGISTRY_ROOT)
    except OSError:
        return None

    try:
        for i in range(subkey_count(root)):
            release_name = winreg.EnumKey(root, i)
            try:
                release_key = winreg.OpenKey(root, release_name)
            except OSError:
                continue
            try:
                result = _scan_installed_products(release_key, code, cfg, version)
                if result is not None:
                    return result
            finally:
                winreg.CloseKey(release_key)
    finally:
        winreg.CloseKey(root)
    return None


def _scan_installed_products(
    release_key: winreg.HKEYType, code: str, cfg: HostConfig, version: str
) -> str | None:
    try:
        products = winreg.OpenKey(release_key, ACAD_INSTALLED_PRODUCTS)
    except OSError:
        return None
    try:
        try:
            install_dir, _ = winreg.QueryValueEx(products, "")
        except OSError:
            return None
        if not isinstance(install_dir, str) or not install_dir.strip():
            return None
        if version_from_install_dir(install_dir) != version:
            return None
        try:
            sub = winreg.OpenKey(products, code)
        except OSError:
            return None
        winreg.CloseKey(sub)
        assert cfg.exe_name is not None
        exe = os.path.join(install_dir.rstrip("\\/"), cfg.exe_name)
        return exe if os.path.isfile(exe) else None
    finally:
        winreg.CloseKey(products)


def _from_filesystem(cfg: HostConfig, version: str) -> str | None:
    program_files = os.environ.get("ProgramFiles", r"C:\Program Files")
    autodesk_dir = os.path.join(program_files, "Autodesk")
    if not os.path.isdir(autodesk_dir):
        return None

    patterns = [f"AutoCAD {version}", f"AutoCAD {version} *"]
    for pattern in patterns:
        for entry in sorted(Path(autodesk_dir).glob(pattern)):
            assert cfg.exe_name is not None
            exe = entry / cfg.exe_name
            if exe.is_file():
                return str(exe)
    return None
