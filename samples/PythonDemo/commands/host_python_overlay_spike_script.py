"""Spike: inject AppData overlay into Plant attach. Do not touch PLNT3D.

Requires a pre-staged overlay (stdlib .pyd + python3.dll + packaging):
  %APPDATA%\\RevitDevTool\\overlays\\cp313-win_amd64

Re-stage from a CPython 3.13 Windows install (example uv layout)::

    $uv = ...\\cpython-3.13-windows-x86_64-none
    $o  = $env:APPDATA\\RevitDevTool\\overlays\\cp313-win_amd64
    Copy python3.dll, DLLs\\_ssl.pyd, _ctypes.pyd, _hashlib.pyd,
         libssl-3-x64.dll, libcrypto-3-x64.dll, libffi-8.dll
    uv pip install --target $o --python-version 3.13 --only-binary=:all: packaging
"""

from __future__ import annotations

import importlib
import os
import site
import sys

OVERLAY = os.path.join(
    os.environ.get("APPDATA", ""),
    "RevitDevTool",
    "overlays",
    "cp313-win_amd64",
)


def _try_import(name: str) -> str:
    try:
        mod = importlib.import_module(name)
        origin = getattr(mod, "__file__", None) or "(builtin/unknown)"
        return f"ok  {origin}"
    except Exception as ex:
        return f"FAIL {type(ex).__name__}: {ex}"


def _load_native(path: str) -> str:
    try:
        from System.Runtime.InteropServices import NativeLibrary
        NativeLibrary.Load(path)
        return "ok"
    except Exception as ex:
        return f"FAIL {type(ex).__name__}: {ex}"


def _mapped() -> None:
    try:
        from System.Diagnostics import Process
    except Exception as ex:
        print("mapped FAIL", type(ex).__name__, ex)
        return
    for module in Process.GetCurrentProcess().Modules:
        path = module.FileName or ""
        lower = path.lower()
        if "python" in lower or lower.endswith(".pyd") or "libssl" in lower or "libcrypto" in lower or "libffi" in lower:
            print(" ", path)


def main() -> None:
    print("overlay", OVERLAY, "exists=", os.path.isdir(OVERLAY))
    if not os.path.isdir(OVERLAY):
        print("stage the overlay first; abort")
        return

    python3 = os.path.join(OVERLAY, "python3.dll")
    print("LoadLibrary python3.dll", _load_native(python3))

    if hasattr(os, "add_dll_directory"):
        cookie = os.add_dll_directory(OVERLAY)
        print("add_dll_directory", OVERLAY, "cookie", cookie)
    else:
        print("add_dll_directory missing")

    if OVERLAY not in sys.path:
        site.addsitedir(OVERLAY)
    importlib.invalidate_caches()

    print()
    print("sys.path tail")
    for i, p in enumerate(sys.path):
        if i < 4:
            continue
        print(f"  [{i}] {p!r}")

    print()
    print("mapped after inject")
    _mapped()
    print()
    for name in (
        "packaging",
        "packaging.version",
        "_ctypes",
        "ctypes",
        "select",
        "_socket",
        "socket",
        "_ssl",
        "ssl",
        "_hashlib",
        "clr",
    ):
        print(f"import {name:<20} {_try_import(name)}")


if __name__ == "__main__":
    main()
