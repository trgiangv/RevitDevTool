"""Parse MCP tool definitions from Python source files using FastMCP.

Scans a directory for *mcp.py files that create FastMCP instances with @mcp.tool()
decorators. Uses FastMCP.list_tools() to extract definitions without executing
any tool logic.

Tool files must not import Revit SDK at module level. Use lazy imports inside
function bodies so this parser can load them outside the Revit process:

    @mcp.tool()
    async def my_tool() -> str:
        from Nice3point.Revit.Toolkit import Context  # lazy - Revit process only
        return Context.ActiveDocument.Title

The parser accepts module-level `mcp`, `get_mcp_server()`, or other objects that
behave like an MCP server by exposing `list_tools()`.

Output: JSON array compatible with C# McpToolDefinition.

Usage:
    python tools_parser.py <toolset_dir>
"""

from __future__ import annotations

import argparse
import asyncio
import contextlib
import importlib.util
import inspect
import json
import sys
from pathlib import Path
from types import ModuleType

from mcp.server.fastmcp import FastMCP

try:
    from .schemas.models import ToolDefinition
except ImportError:
    from schemas.models import ToolDefinition


def _looks_like_mcp_server(obj):
    return obj is not None and not inspect.isclass(obj) and hasattr(obj, "list_tools") and callable(getattr(obj, "list_tools"))


def _iter_module_mcp_candidates(module: ModuleType):
    get_mcp_server = getattr(module, "get_mcp_server", None)
    if callable(get_mcp_server):
        try:
            candidate = get_mcp_server()
            if _looks_like_mcp_server(candidate):
                yield candidate
        except Exception:
            pass

    module_level = getattr(module, "mcp", None)
    if _looks_like_mcp_server(module_level):
        yield module_level
def _find_mcp_servers(module: ModuleType):
    yielded_ids: set[int] = set()

    for candidate in _iter_module_mcp_candidates(module):
        candidate_id = id(candidate)
        if candidate_id in yielded_ids:
            continue
        yielded_ids.add(candidate_id)
        yield candidate

    for _name, obj in inspect.getmembers(module):
        if _looks_like_mcp_server(obj):
            candidate_id = id(obj)
            if candidate_id not in yielded_ids:
                yielded_ids.add(candidate_id)
                yield obj


def _get_attr(obj, *names):
    for name in names:
        if hasattr(obj, name):
            return getattr(obj, name)
    return None


def _extract_tools(mcp_instance: FastMCP, source_path: str, container_type: str) -> list[dict]:
    tools = asyncio.run(mcp_instance.list_tools())
    server_name = _get_attr(mcp_instance, "name") or container_type
    definitions: list[dict] = []
    for t in tools:
        annotations = _serialize_metadata(_get_attr(t, "annotations"))
        meta = _serialize_metadata(_get_attr(t, "meta"))
        output_schema = _get_attr(t, "outputSchema", "output_schema")
        definition = ToolDefinition(
            **{
                "name": t.name,
                "description": (t.description or "").strip(),
                "inputSchemaJson": json.dumps(_get_attr(t, "inputSchema", "input_schema") or {}),
                "outputSchemaJson": json.dumps(output_schema) if output_schema is not None else None,
                "annotationsJson": json.dumps(annotations) if annotations is not None else None,
                "metaJson": json.dumps(meta) if meta is not None else None,
                "structuredOutput": True,
                "sourceKind": "Python",
                "containerType": str(server_name),
                "methodName": t.name,
                "sourcePath": source_path,
            }
        )
        definitions.append(definition.model_dump(by_alias=True, exclude_none=True, exclude_defaults=True))

    return definitions


def _serialize_metadata(value: object) -> object:
    if value is None:
        return None
    if hasattr(value, "model_dump"):
        return value.model_dump(by_alias=True, exclude_none=True)
    return value
@contextlib.contextmanager
def _module_import_scope(toolset_dir: Path, source_dir: Path):
    inserted_paths: list[str] = []
    for path in (str(source_dir), str(toolset_dir)):
        if path in sys.path:
            continue
        sys.path.insert(0, path)
        inserted_paths.append(path)

    try:
        yield
    finally:
        for path in inserted_paths:
            try:
                sys.path.remove(path)
            except ValueError:
                pass


def _iter_tool_files(toolset_path: Path):
    if toolset_path.is_file():
        if toolset_path.name.startswith("_") or not toolset_path.match("*mcp.py"):
            return
        yield toolset_path
        return

    for py_file in sorted(toolset_path.rglob("*mcp.py")):
        if py_file.name.startswith("_"):
            continue
        yield py_file


def parse_path(toolset_path: Path) -> list[dict]:
    all_tools: list[dict] = []
    seen_instances: set[int] = set()
    root_dir = toolset_path.parent if toolset_path.is_file() else toolset_path
    for py_file in _iter_tool_files(toolset_path):
        try:
            with _module_import_scope(root_dir, py_file.parent):
                spec = importlib.util.spec_from_file_location(py_file.stem.replace(".", "_"), str(py_file))
                if spec is None or spec.loader is None:
                    continue
                mod = importlib.util.module_from_spec(spec)
                spec.loader.exec_module(mod)

                for mcp_instance in _find_mcp_servers(mod):
                    instance_id = id(mcp_instance)
                    if instance_id in seen_instances:
                        continue
                    seen_instances.add(instance_id)
                    all_tools.extend(
                        _extract_tools(
                            mcp_instance,
                            str(py_file),
                            py_file.name,
                        )
                    )
        except Exception as exc:
            print(f"[WARN] {py_file.name}: {exc}", file=sys.stderr)

    return all_tools


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("toolset_path", help="Directory or .mcp.py file containing FastMCP tool definitions")
    args = parser.parse_args()

    toolset_path = Path(args.toolset_path)
    if not toolset_path.exists():
        print(f"Error: '{toolset_path}' does not exist.", file=sys.stderr)
        sys.exit(1)

    tools = parse_path(toolset_path)
    print(json.dumps(tools))


if __name__ == "__main__":
    main()
