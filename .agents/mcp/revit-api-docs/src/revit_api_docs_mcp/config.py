from __future__ import annotations

import os
from pathlib import Path


SUPPORTED_VERSIONS = tuple(range(2020, 2028))
DEFAULT_VERSION = 2026


def cache_root() -> Path:
    configured = os.environ.get("REVIT_API_DOCS_CACHE")
    if configured:
        return Path(configured).expanduser()

    local_app_data = os.environ.get("LOCALAPPDATA")
    if local_app_data:
        return Path(local_app_data) / "RevitDevTool" / "revit-api-docs-mcp"

    return Path.home() / ".cache" / "revit-api-docs-mcp"


def database_path() -> Path:
    root = cache_root()
    root.mkdir(parents=True, exist_ok=True)
    return root / "revit_api_docs.sqlite3"


def docs_source_root() -> Path | None:
    configured = os.environ.get("REVIT_API_DOCS_SOURCE")
    if not configured:
        return None

    candidate = Path(configured).expanduser()
    if (candidate / "README.md").exists() and any((candidate / f"{year}.htm").exists() for year in SUPPORTED_VERSIONS):
        return candidate
    return None


def validate_version(version: int) -> int:
    if version not in SUPPORTED_VERSIONS:
        versions = ", ".join(str(value) for value in SUPPORTED_VERSIONS)
        raise ValueError(f"Unsupported Revit API docs version {version}. Supported: {versions}.")
    return version
