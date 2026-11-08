"""Autodesk version vocabulary.

Revit and the AutoCAD family are addressed by a calendar year (``2026``),
the same token used by ``--host-version`` and pipe names. The floor matches
``HostVersions.AutodeskMinimal``. Other hosts do not use this pattern.

An Autodesk install folder matches when its last whitespace-separated token
is that year. ``AutoCAD 2026`` and ``Civil 3D 2026`` both yield ``2026``.
``AutoCAD 12026`` does not.
"""

from __future__ import annotations

import os
from typing import Final

AUTODESK_VERSION_MINIMAL: Final = 2022


def is_autodesk_version(version: str) -> bool:
    """Return True when *version* is an Autodesk calendar-year token."""
    if len(version) != 4 or not version.isdigit():
        return False
    return int(version) >= AUTODESK_VERSION_MINIMAL


def version_from_folder(folder: str) -> str | None:
    """Return the Autodesk year at the end of an install folder name."""
    name = folder.strip()
    if not name:
        return None
    token = name.rsplit(maxsplit=1)[-1]
    return token if is_autodesk_version(token) else None


def version_from_install_dir(install_dir: str) -> str | None:
    """Return the Autodesk year of an install directory."""
    folder = os.path.basename(install_dir.rstrip("\\/"))
    return version_from_folder(folder)
