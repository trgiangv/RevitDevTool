"""Filesystem path validation for export tools."""
from __future__ import annotations

import os
import re
import tempfile
import uuid

from shared.responses import ToolError

_BLOCKED_DIR_NAMES = frozenset({
    "windows",
    "program files",
    "program files (x86)",
    "system32",
})


def _validate_path_safety(raw_path: str, resolved_path: str) -> None:
    segments = re.split(r"[/\\]+", raw_path.strip())
    if ".." in segments:
        raise ToolError("Path traversal ('..') is not allowed", code="path.unsafe")

    normalized = raw_path.replace("\\", "/")
    if "//" in normalized or re.search(r"(?<![A-Za-z]:)[/\\]{2,}", raw_path):
        raise ToolError("Double slashes in path are not allowed", code="path.unsafe")

    parts_lower = [p.lower() for p in os.path.normpath(resolved_path).split(os.sep) if p]
    for part in parts_lower:
        if part in _BLOCKED_DIR_NAMES:
            raise ToolError("Access to system directory '{}' is denied".format(part), code="path.unsafe")


def sanitize_directory_path(directory: str) -> str:
    raw = directory.strip()
    if not raw:
        raise ToolError("Directory path cannot be empty", code="path.empty")
    path = os.path.abspath(raw)
    _validate_path_safety(raw, path)
    return path


def create_directory(directory: str) -> None:
    os.makedirs(sanitize_directory_path(directory), exist_ok=True)


def sanitize_file_path(file_path: str) -> str:
    raw = file_path.strip()
    path = os.path.abspath(raw)
    _validate_path_safety(raw, path)
    parent = os.path.dirname(path)
    if parent:
        os.makedirs(parent, exist_ok=True)
    return path


def generate_unique_file_path(directory: str, base_name: str, extension: str) -> str:
    safe_dir = sanitize_directory_path(directory)
    os.makedirs(safe_dir, exist_ok=True)
    safe_base = "".join(c if c.isalnum() or c in "-_" else "_" for c in base_name)[:80]
    filename = "{}_{}.{}".format(safe_base, uuid.uuid4().hex[:8], extension.lstrip("."))
    return os.path.join(safe_dir, filename)


def default_export_dir() -> str:
    folder = os.path.join(tempfile.gettempdir(), "RevitMCPExports")
    os.makedirs(folder, exist_ok=True)
    return folder
