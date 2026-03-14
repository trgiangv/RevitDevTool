"""In-process MCP primitive parser for Python.NET (mcp == 1.26.0).

Supports the two official authoring workflows exposed by the Python SDK:
``FastMCP`` and the low-level ``Server`` API.

Output format: SDK-shaped JSON where the ``protocol`` field is a direct
``model_dump()`` of the Python SDK object (Tool, Prompt, Resource/ResourceTemplate)
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
from pathlib import Path
from typing import Any, Callable, Generator, Mapping, TypeAlias

from mcp.server.fastmcp import FastMCP
from mcp.server.lowlevel import Server as LowLevelServer
from mcp.types import (
    ListPromptsRequest,
    ListResourceTemplatesRequest,
    ListResourcesRequest,
    ListToolsRequest,
    Prompt,
    Resource,
    ResourceTemplate,
    Tool,
)

PrimitiveServer: TypeAlias = FastMCP | LowLevelServer[Any]
LowLevelRequestHandler: TypeAlias = Callable[[Any], Any]

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
        if isinstance(obj, (FastMCP, LowLevelServer)):
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


def _build_prompt_entry(prompt: Prompt, server_name: str, source_path: str) -> EntryDict:
    return {
        "protocol": _dump_protocol(prompt),
        "binding": _make_binding(server_name, str(prompt.name), source_path),
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


def _server_name(server: PrimitiveServer, fallback: str = "FastMCP") -> str:
    return server.name if server.name else fallback


def _extract_fastmcp_tools(server: FastMCP, source_path: str) -> list[EntryDict]:
    tools: list[Tool] = asyncio.run(server.list_tools())
    name = _server_name(server)
    return [_build_tool_entry(t, name, source_path) for t in tools]


def _extract_fastmcp_prompts(server: FastMCP, source_path: str) -> list[EntryDict]:
    prompts: list[Prompt] = asyncio.run(server.list_prompts())
    name = _server_name(server)
    return [_build_prompt_entry(p, name, source_path) for p in prompts]


def _extract_fastmcp_resources(server: FastMCP, source_path: str) -> list[EntryDict]:
    name = _server_name(server)
    entries: list[EntryDict] = []
    direct: list[Resource] = asyncio.run(server.list_resources())
    entries.extend(_build_resource_entry(r, name, source_path) for r in direct)
    templates: list[ResourceTemplate] = asyncio.run(server.list_resource_templates())
    entries.extend(_build_resource_entry(t, name, source_path) for t in templates)
    return entries


def _get_lowlevel_request_handlers(server: LowLevelServer[Any]) -> Mapping[type[Any], LowLevelRequestHandler]:
    return server.request_handlers


def _run_lowlevel_handler(handler: LowLevelRequestHandler, request: Any) -> Any:
    return asyncio.run(handler(request))


def _extract_lowlevel_tools(server: LowLevelServer[Any], source_path: str) -> list[EntryDict]:
    handler = _get_lowlevel_request_handlers(server).get(ListToolsRequest)
    if handler is None:
        return []
    result = _run_lowlevel_handler(handler, ListToolsRequest())
    tools = result.root.tools if result is not None else []
    name = _server_name(server, "Server")
    return [_build_tool_entry(t, name, source_path) for t in tools]


def _extract_lowlevel_prompts(server: LowLevelServer[Any], source_path: str) -> list[EntryDict]:
    handler = _get_lowlevel_request_handlers(server).get(ListPromptsRequest)
    if handler is None:
        return []
    result = _run_lowlevel_handler(handler, ListPromptsRequest())
    prompts = result.root.prompts if result is not None else []
    name = _server_name(server, "Server")
    return [_build_prompt_entry(p, name, source_path) for p in prompts]


def _extract_lowlevel_resources(server: LowLevelServer[Any], source_path: str) -> list[EntryDict]:
    entries: list[EntryDict] = []
    name = _server_name(server, "Server")
    handlers = _get_lowlevel_request_handlers(server)

    list_handler = handlers.get(ListResourcesRequest)
    if list_handler is not None:
        result = _run_lowlevel_handler(list_handler, ListResourcesRequest())
        resources = result.root.resources if result is not None else []
        entries.extend(_build_resource_entry(r, name, source_path) for r in resources)

    template_handler = handlers.get(ListResourceTemplatesRequest)
    if template_handler is not None:
        result = _run_lowlevel_handler(template_handler, ListResourceTemplatesRequest())
        templates = result.root.resourceTemplates if result is not None else []
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
        prompts: list[EntryDict] = []
        resources: list[EntryDict] = []
        for server in _find_servers(mod):
            if id(server) in seen_servers:
                continue
            seen_servers.add(id(server))
            if isinstance(server, FastMCP):
                tools.extend(_extract_fastmcp_tools(server, str(py_file)))
                prompts.extend(_extract_fastmcp_prompts(server, str(py_file)))
                resources.extend(_extract_fastmcp_resources(server, str(py_file)))
            else:
                tools.extend(_extract_lowlevel_tools(server, str(py_file)))
                prompts.extend(_extract_lowlevel_prompts(server, str(py_file)))
                resources.extend(_extract_lowlevel_resources(server, str(py_file)))
        return {"tools": tools, "prompts": prompts, "resources": resources}
    except Exception:  # noqa: BLE001
        traceback.print_exc()
        return {"tools": [], "prompts": [], "resources": []}
    finally:
        _remove_sys_path(inserted)


def parse_directory(toolset_path: str) -> str:
    root = Path(toolset_path)
    root_dir: Path = root.parent if root.is_file() else root
    seen_servers: set[int] = set()
    catalog: CatalogDict = {"tools": [], "prompts": [], "resources": []}

    for py_file in _iter_tool_files(root):
        extracted = _extract_from_file(py_file, root_dir, seen_servers)
        catalog["tools"].extend(extracted["tools"])
        catalog["prompts"].extend(extracted["prompts"])
        catalog["resources"].extend(extracted["resources"])

    return json.dumps(catalog)


if "__toolset_directory__" in dir():
    __parser_result__ = parse_directory(__toolset_directory__)  # noqa: F821
elif __name__ == "__main__":
    if len(sys.argv) != 2:
        print(f"Usage: {sys.argv[0]} <toolset_path>", file=sys.stderr)
        sys.exit(1)
    print(parse_directory(sys.argv[1]))
