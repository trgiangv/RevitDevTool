"""Auto-dismiss Revit startup dialogs via Win32 polling.

Ported from ``RevitDevTool.Console.Services.Hosting.StartupDialogResolver``.
Runs in a background thread and clicks whitelisted buttons on known dialog
windows belonging to the Revit process.
"""

from __future__ import annotations

import ctypes
import ctypes.wintypes
import threading
from dataclasses import dataclass, field

_BM_CLICK = 0x00F5
_DIALOG_CLASS = "#32770"
_BUTTON_CLASS = "button"

_user32 = ctypes.windll.user32  # type: ignore[attr-defined]

_EnumWindowsProc = ctypes.WINFUNCTYPE(
    ctypes.wintypes.BOOL, ctypes.wintypes.HWND, ctypes.wintypes.LPARAM,
)


def _is_window_visible(hwnd: int) -> bool:
    return bool(_user32.IsWindowVisible(hwnd))


def _get_window_text(hwnd: int) -> str:
    length = _user32.GetWindowTextLengthW(hwnd)
    if length <= 0:
        return ""
    buf = ctypes.create_unicode_buffer(length + 1)
    _user32.GetWindowTextW(hwnd, buf, len(buf))
    return buf.value


def _get_class_name(hwnd: int) -> str:
    buf = ctypes.create_unicode_buffer(256)
    _user32.GetClassNameW(hwnd, buf, 256)
    return buf.value


def _get_window_pid(hwnd: int) -> int:
    pid = ctypes.wintypes.DWORD()
    _user32.GetWindowThreadProcessId(hwnd, ctypes.byref(pid))
    return pid.value


def _send_click(hwnd: int) -> None:
    _user32.SendMessageW(hwnd, _BM_CLICK, 0, 0)


@dataclass
class DialogResolverOptions:
    poll_interval_s: float = 0.5
    dialog_title_keywords: list[str] = field(default_factory=lambda: [
        "Autodesk", "Revit", "Load", "Security", "Warning", "Add-in", "Addin",
    ])
    preferred_button_keywords: list[str] = field(default_factory=lambda: [
        "Always Load", "Load", "OK", "Yes", "Accept", "Continue", "Close",
    ])


class StartupDialogResolver:
    """Poll for Revit startup dialogs and auto-click whitelisted buttons."""

    def __init__(self, process_id: int, options: DialogResolverOptions | None = None) -> None:
        self._pid = process_id
        self._opts = options or DialogResolverOptions()
        self._stop = threading.Event()
        self._thread: threading.Thread | None = None
        self._clicked: set[int] = set()

    def start(self) -> None:
        if self._thread is not None:
            return
        self._stop.clear()
        self._thread = threading.Thread(target=self._run, daemon=True, name="revit-dialog-resolver")
        self._thread.start()

    def stop(self) -> None:
        self._stop.set()
        if self._thread is not None:
            self._thread.join(timeout=5.0)
            self._thread = None

    def _run(self) -> None:
        while not self._stop.is_set():
            self._scan()
            self._stop.wait(self._opts.poll_interval_s)

    def _scan(self) -> None:
        for hwnd in self._enum_dialog_windows():
            title = _get_window_text(hwnd)
            if not title or not self._is_whitelisted(title):
                continue
            button = self._find_button(hwnd)
            if button and button not in self._clicked:
                _send_click(button)
                self._clicked.add(button)

    def _is_whitelisted(self, title: str) -> bool:
        lower = title.lower()
        return any(kw.lower() in lower for kw in self._opts.dialog_title_keywords)

    def _find_button(self, parent: int) -> int | None:
        result: list[int] = []

        @_EnumWindowsProc
        def _cb(child: int, _: int) -> bool:
            if _is_preferred_button(child, self._opts.preferred_button_keywords):
                result.append(child)
                return False
            return True

        _user32.EnumChildWindows(parent, _cb, 0)
        return result[0] if result else None

    def _enum_dialog_windows(self) -> list[int]:
        windows: list[int] = []

        @_EnumWindowsProc
        def _cb(hwnd: int, _: int) -> bool:
            if _is_target_dialog(hwnd, self._pid):
                windows.append(hwnd)
            return True

        _user32.EnumWindows(_cb, 0)
        return windows


def _is_preferred_button(hwnd: int, keywords: list[str]) -> bool:
    if _get_class_name(hwnd).lower() != _BUTTON_CLASS:
        return False
    text = _get_window_text(hwnd)
    if not text:
        return False
    text_lower = text.lower()
    return any(kw.lower() in text_lower for kw in keywords)


def _is_target_dialog(hwnd: int, target_pid: int) -> bool:
    if not _is_window_visible(hwnd):
        return False
    if _get_class_name(hwnd) != _DIALOG_CLASS:
        return False
    return _get_window_pid(hwnd) == target_pid
