"""Per-host executable resolvers.

``discovery`` finds pipes and launches a process. Which file to launch lives
here, one module per install layout.
"""

from __future__ import annotations

from ..constants import get_host_config
from .acad import find_acad_executable, product_code
from .revit import find_revit_executable
from .revit import supports as revit_supports

__all__ = ["find_executable"]


def find_executable(host_name: str, version: str) -> str | None:
    """Return the host executable for *version*, or None when it is not installed.

    *host_name* is the only key. Hosts with no ``exe_name`` are pipe-only.
    """
    cfg = get_host_config(host_name)
    if cfg.exe_name is None:
        return None
    key = host_name.strip().lower()
    if revit_supports(key):
        return find_revit_executable(version)
    if product_code(key) is not None:
        return find_acad_executable(key, cfg, version)
    return None
