"""64-bit HKLM helpers shared by host resolvers."""

from __future__ import annotations

import winreg


def open_hklm(path: str) -> winreg.HKEYType:
    """Open *path* in the 64-bit registry view."""
    return winreg.OpenKey(
        winreg.HKEY_LOCAL_MACHINE,
        path,
        0,
        winreg.KEY_READ | winreg.KEY_WOW64_64KEY,
    )


def subkey_count(key: winreg.HKEYType) -> int:
    """Return the subkey count. QueryInfoKey is (subkeys, values, mtime)."""
    try:
        count, _, _ = winreg.QueryInfoKey(key)
        return count
    except OSError:
        return 0
