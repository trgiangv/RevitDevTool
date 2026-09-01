"""Dump the in-host CPython layout. No PEP 723 — runs on Plant attach.

Plant embed has no _ctypes.pyd, so this file must not import ctypes.
"""

from __future__ import annotations

import importlib
import importlib.machinery
import os
import sys

try:
    import sysconfig
except Exception as ex:
    sysconfig = None
    _sysconfig_error = f"{type(ex).__name__}: {ex}"
else:
    _sysconfig_error = None


def _try_import(name: str) -> str:
    try:
        mod = importlib.import_module(name)
        origin = getattr(mod, "__file__", None) or "(builtin)"
        return f"ok  {origin}"
    except Exception as ex:
        return f"FAIL {type(ex).__name__}: {ex}"


def _print_mapped_python_modules() -> None:
    try:
        from System.Diagnostics import Process
    except Exception as ex:
        print("mapped modules  FAIL", type(ex).__name__, ex)
        return

    for module in Process.GetCurrentProcess().Modules:
        path = module.FileName or ""
        lower = path.lower()
        if "python" in lower or lower.endswith(".pyd"):
            print(" ", path)


def main() -> None:
    print("version     ", sys.version)
    print("hexversion  ", hex(sys.hexversion))
    print("executable  ", sys.executable)
    print("prefix      ", sys.prefix)
    print("base_prefix ", getattr(sys, "base_prefix", None))
    print("exec_prefix ", sys.exec_prefix)
    print("dllhandle   ", hex(getattr(sys, "dllhandle", 0)))
    print("PYTHONHOME  ", os.environ.get("PYTHONHOME"))
    print("PYTHONPATH  ", os.environ.get("PYTHONPATH"))
    if _sysconfig_error:
        print("sysconfig   ", "FAIL", _sysconfig_error)
    else:
        print("platform    ", sysconfig.get_platform())
        print("gil_disabled", sysconfig.get_config_var("Py_GIL_DISABLED"))
    print("ext_suffix  ", importlib.machinery.EXTENSION_SUFFIXES)
    print("debug       ", hasattr(sys, "gettotalrefcount"))
    print()
    print("sys.path")
    for i, p in enumerate(sys.path):
        exists = os.path.exists(p) if p else False
        print(f"  [{i}] {p!r}  exists={exists}")
    print()
    print("mapped python / pyd")
    _print_mapped_python_modules()
    print()
    for name in ("ssl", "_ssl", "ctypes", "_ctypes", "clr", "pythonnet"):
        print(f"import {name:<12} {_try_import(name)}")


if __name__ == "__main__":
    main()
