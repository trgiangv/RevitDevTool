from __future__ import annotations

import importlib.util
import json
from pathlib import Path
from types import ModuleType

import pytest


def _load_module(file_path: Path, module_name: str) -> ModuleType:
    spec = importlib.util.spec_from_file_location(module_name, file_path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load module from {file_path}")

    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


@pytest.fixture(scope="session")
def repo_root() -> Path:
    return Path(__file__).resolve().parents[2]


@pytest.fixture(scope="session")
def parser_module(repo_root: Path) -> ModuleType:
    parser_path = repo_root / "source" / "DevTools.Execution" / "Resources" / "scripts" / "ToolParser.py"
    return _load_module(parser_path, "rdt_tool_parser_tests")


@pytest.fixture(scope="session")
def sample_toolset_dir(repo_root: Path) -> Path:
    return repo_root / "samples" / "PythonDemo" / "mcp_toolset"


@pytest.fixture(scope="session")
def parsed_catalog(parser_module: ModuleType, sample_toolset_dir: Path) -> dict[str, list[dict[str, object]]]:
    return json.loads(parser_module.parse_directory(str(sample_toolset_dir)))