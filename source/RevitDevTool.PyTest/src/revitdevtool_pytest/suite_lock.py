"""Suite-level mutex and context resolution.

Provides a Windows named Mutex to prevent two pytest processes from
running the same test suite concurrently, plus helpers to resolve
which ``conftest.py`` defines a suite.
"""

from __future__ import annotations

import ctypes
import hashlib
from pathlib import Path

import pytest

from .constants import EXIT_CODE_CONFIG_ERROR, PLUGIN_NAME

_ERROR_ALREADY_EXISTS = 183


class SuiteMutex:
    """RAII wrapper around a Windows named Mutex."""

    def __init__(self) -> None:
        self._handle: int | None = None

    @property
    def acquired(self) -> bool:
        return self._handle is not None

    def acquire(self, suite_key: str) -> bool:
        """Try to acquire the mutex. Returns False if another process holds it."""
        if self._handle is not None:
            return True

        mutex_name = f"Global\\RevitDevTool_PyTest_{suite_key}"
        handle = ctypes.windll.kernel32.CreateMutexW(None, True, mutex_name)  # type: ignore[attr-defined]
        if handle == 0:
            return False

        if ctypes.windll.kernel32.GetLastError() == _ERROR_ALREADY_EXISTS:  # type: ignore[attr-defined]
            ctypes.windll.kernel32.CloseHandle(handle)  # type: ignore[attr-defined]
            return False

        self._handle = handle
        return True

    def release(self) -> None:
        if self._handle is None:
            return
        ctypes.windll.kernel32.ReleaseMutex(self._handle)  # type: ignore[attr-defined]
        ctypes.windll.kernel32.CloseHandle(self._handle)  # type: ignore[attr-defined]
        self._handle = None


def resolve_suite_context(session: pytest.Session) -> tuple[str, str]:
    """Return ``(suite_key, suite_path)`` for the current session's items."""
    root_path = Path(str(session.config.rootpath)).resolve()
    conftest_paths = {
        nearest_conftest(Path(str(item.path)).resolve(), root_path)
        for item in session.items
    }
    if len(conftest_paths) > 1:
        formatted = ", ".join(sorted(str(path) for path in conftest_paths))
        pytest.exit(
            f"{PLUGIN_NAME}: A run can target only one conftest.py suite. Found: {formatted}",
            returncode=EXIT_CODE_CONFIG_ERROR,
        )

    suite_path = conftest_paths.pop() if conftest_paths else root_path
    suite_path_text = str(suite_path)
    return suite_key_for_path(suite_path_text), suite_path_text


def nearest_conftest(file_path: Path, root_path: Path) -> Path:
    current = file_path.parent
    normalized_root = root_path.resolve()
    while True:
        candidate = current / "conftest.py"
        if candidate.is_file():
            return candidate.resolve()
        if current == normalized_root or current.parent == current:
            return normalized_root
        current = current.parent


def suite_key_for_path(path: str) -> str:
    normalized = str(Path(path).resolve()).lower()
    digest = hashlib.sha1(normalized.encode("utf-8")).hexdigest()
    return digest[:16]
