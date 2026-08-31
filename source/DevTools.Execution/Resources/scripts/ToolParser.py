"""In-process MCP primitive parser for Python.NET (mcp == 2.0.0).

Supports the two official authoring workflows exposed by the Python SDK:
``MCPServer`` and the low-level ``Server`` API.

Output format: SDK-shaped JSON where the ``protocol`` field is a direct
``model_dump()`` of the Python SDK object (Tool, Resource/ResourceTemplate)
and ``binding`` carries the metadata needed for C# invocation.
"""
import asyncio
import contextlib
import importlib.util
import inspect
import json
import sys
import traceback
import types
import uuid
from collections.abc import Generator
from pathlib import Path
from typing import Any, TypeAlias, cast

from mcp.server.context import ServerRequestContext
from mcp.server.lowlevel import Server as LowLevelServer
from mcp.server.mcpserver import MCPServer
from mcp.types import Resource, ResourceTemplate, Tool
from mcp.types.version import LATEST_HANDSHAKE_VERSION

PrimitiveServer: TypeAlias = MCPServer | LowLevelServer[Any]

_SCOPE_TOOLSET_DIRECTORY = "__toolset_directory__"
_SCOPE_PARSER_RESULT = "__parser_result__"

_LOWLEVEL_METHOD_LIST_TOOLS = "tools/list"
_LOWLEVEL_METHOD_LIST_RESOURCES = "resources/list"
_LOWLEVEL_METHOD_LIST_RESOURCE_TEMPLATES = "resources/templates/list"

EntryDict: TypeAlias = dict[str, Any]
CatalogDict: TypeAlias = dict[str, list[EntryDict]]


def _load_module(file_path: str) -> types.ModuleType:
    name = f"rdt_parser_{uuid.uuid4().hex}"
    spec = importlib.util.spec_from_file_location(name, file_path)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"Cannot load module: {file_path}")
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def _find_servers(module: types.ModuleType) -> list[PrimitiveServer]:
    seen: set[int] = set()
    result: list[PrimitiveServer] = []

    def _collect(obj: object) -> None:
        if obj is None or inspect.isclass(obj) or id(obj) in seen:
            return
        if isinstance(obj, (MCPServer, LowLevelServer)):
            seen.add(id(obj))
            result.append(obj)

    for obj in vars(module).values():
        _collect(obj)

    return result


def _make_binding(server_name: str, method_name: str, source_path: str) -> dict[str, str]:
    return {
        "containerType": server_name,
        "methodName": method_name,
        "sourcePath": source_path,
    }


def _dump_protocol(model: Any) -> dict[str, Any]:
    """Serialize an SDK protocol model to a JSON-compatible dict."""
    return model.model_dump(mode="json", exclude_none=True, by_alias=True)


def _build_tool_entry(tool: Tool, server_name: str, source_path: str) -> EntryDict:
    return {
        "protocol": _dump_protocol(tool),
        "binding": _make_binding(server_name, tool.name, source_path),
    }


def _build_resource_entry(
    resource: Resource | ResourceTemplate,
    server_name: str,
    source_path: str,
) -> EntryDict:
    is_template = isinstance(resource, ResourceTemplate)
    return {
        "protocol": _dump_protocol(resource),
        "isTemplate": is_template,
        "binding": _make_binding(server_name, str(resource.name or ""), source_path),
    }


def _server_name(server: PrimitiveServer, fallback: str = "MCPServer") -> str:
    return server.name if server.name else fallback


def _make_lowlevel_context(method: str) -> ServerRequestContext[Any]:
    return ServerRequestContext(
        session=cast(Any, None),
        lifespan_context={},
        protocol_version=LATEST_HANDSHAKE_VERSION,
        method=method,
    )


def _run_lowlevel_handler(
    server: LowLevelServer[Any],
    method: str,
    params: Any | None = None,
) -> Any:
    entry = server.get_request_handler(method)
    if entry is None:
        return None
    if params is None:
        params = entry.params_type()
    ctx = _make_lowlevel_context(method)
    return asyncio.run(entry.handler(ctx, params))


def _extract_mcpserver_tools(server: MCPServer, source_path: str) -> list[EntryDict]:
    tools: list[Tool] = asyncio.run(server.list_tools())
    name = _server_name(server)
    return [_build_tool_entry(t, name, source_path) for t in tools]


def _extract_mcpserver_resources(server: MCPServer, source_path: str) -> list[EntryDict]:
    name = _server_name(server)
    entries: list[EntryDict] = []
    direct: list[Resource] = asyncio.run(server.list_resources())
    entries.extend(_build_resource_entry(r, name, source_path) for r in direct)
    templates: list[ResourceTemplate] = asyncio.run(server.list_resource_templates())
    entries.extend(_build_resource_entry(t, name, source_path) for t in templates)
    return entries


def _extract_lowlevel_tools(server: LowLevelServer[Any], source_path: str) -> list[EntryDict]:
    result = _run_lowlevel_handler(server, _LOWLEVEL_METHOD_LIST_TOOLS)
    tools = result.tools if result is not None else []
    name = _server_name(server, "Server")
    return [_build_tool_entry(t, name, source_path) for t in tools]


def _extract_lowlevel_resources(server: LowLevelServer[Any], source_path: str) -> list[EntryDict]:
    entries: list[EntryDict] = []
    name = _server_name(server, "Server")

    list_result = _run_lowlevel_handler(server, _LOWLEVEL_METHOD_LIST_RESOURCES)
    resources = list_result.resources if list_result is not None else []
    entries.extend(_build_resource_entry(r, name, source_path) for r in resources)

    template_result = _run_lowlevel_handler(server, _LOWLEVEL_METHOD_LIST_RESOURCE_TEMPLATES)
    templates = template_result.resource_templates if template_result is not None else []
    entries.extend(_build_resource_entry(t, name, source_path) for t in templates)

    return entries


def _iter_tool_files(toolset_path: Path) -> Generator[Path, None, None]:
    if toolset_path.is_file():
        if not toolset_path.name.startswith("_"):
            yield toolset_path
        return
    for py_file in sorted(toolset_path.rglob("*.py")):
        if not py_file.name.startswith("_"):
            yield py_file


def _prepend_sys_path(*paths: str) -> list[str]:
    inserted: list[str] = []
    for path in paths:
        if path not in sys.path:
            sys.path.insert(0, path)
            inserted.append(path)
    return inserted


def _remove_sys_path(paths: list[str]) -> None:
    for path in paths:
        with contextlib.suppress(ValueError):
            sys.path.remove(path)


def _extract_from_file(
    py_file: Path,
    root_dir: Path,
    seen_servers: set[int],
) -> CatalogDict:
    inserted = _prepend_sys_path(str(root_dir), str(py_file.parent))
    try:
        mod = _load_module(str(py_file))
        tools: list[EntryDict] = []
        resources: list[EntryDict] = []
        for server in _find_servers(mod):
            if id(server) in seen_servers:
                continue
            seen_servers.add(id(server))
            if isinstance(server, MCPServer):
                tools.extend(_extract_mcpserver_tools(server, str(py_file)))
                resources.extend(_extract_mcpserver_resources(server, str(py_file)))
            else:
                tools.extend(_extract_lowlevel_tools(server, str(py_file)))
                resources.extend(_extract_lowlevel_resources(server, str(py_file)))
        return {"tools": tools, "resources": resources}
    except Exception:  # noqa: BLE001
        sys.stderr.write(traceback.format_exc())
        sys.stderr.flush()
        return {"tools": [], "resources": []}
    finally:
        _remove_sys_path(inserted)


def parse_directory(toolset_path: str) -> str:
    root = Path(toolset_path)
    root_dir: Path = root.parent if root.is_file() else root
    seen_servers: set[int] = set()
    catalog: CatalogDict = {"tools": [], "resources": []}

    for py_file in _iter_tool_files(root):
        extracted = _extract_from_file(py_file, root_dir, seen_servers)
        catalog["tools"].extend(extracted["tools"])
        catalog["resources"].extend(extracted["resources"])

    return json.dumps(catalog)


if _SCOPE_TOOLSET_DIRECTORY in dir():
    __parser_result__ = parse_directory(globals()[_SCOPE_TOOLSET_DIRECTORY])
elif __name__ == "__main__":
    if len(sys.argv) != 2:
        print(f"Usage: {sys.argv[0]} <toolset_path>", file=sys.stderr)
        sys.exit(1)
    print(parse_directory(sys.argv[1]))
