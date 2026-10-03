import asyncio
import hashlib
import importlib.util
import json
import sys
import threading
from collections.abc import Mapping
from types import ModuleType
from typing import Any, TypeAlias, cast

from mcp.client import Client
from mcp.server.lowlevel import Server as LowLevelServer
from mcp.server.mcpserver import MCPServer
from pydantic import BaseModel

PrimitiveServer: TypeAlias = MCPServer | LowLevelServer[Any]
InvokeScope: TypeAlias = Mapping[str, object]
CacheDict: TypeAlias = dict[tuple[str, int], dict[str, object]]

_SCOPE_FILE = "__file__"
_SCOPE_ROOT = "__root__"
_SCOPE_SOURCE_FILE = "__source_file__"
_SCOPE_OPERATION = "__operation__"
_SCOPE_TOOL_NAME = "__tool_name__"
_SCOPE_PAYLOAD_JSON = "__payload_json__"
_SCOPE_RESOURCE_URI = "__resource_uri__"
_SCOPE_CACHE = "__mcp_client_cache__"
_SCOPE_MTIME_TICKS = "__mtime_ticks__"

_OP_TOOL = "tool"
_OP_RESOURCE = "resource"
_OP_ENSURE_SERVER = "ensure_server"
_OP_CLEAR_CACHE = "clear_cache"


def __dump_mcp_result(result: object) -> dict[str, object]:
    if isinstance(result, BaseModel):
        dumped = result.model_dump(by_alias=True, exclude_none=True)
        if isinstance(dumped, dict):
            return dumped
        raise RuntimeError(f"Unexpected MCP result dump type: {type(dumped)!r}")
    raise RuntimeError(f"Unexpected MCP result type: {type(result)!r}")


def __read_scope_string(scope: InvokeScope, key: str, default: str = "") -> str:
    value = scope.get(key, default)
    return value if isinstance(value, str) else default


def __read_scope_int(scope: InvokeScope, key: str) -> int:
    value = scope.get(key, 0)
    if isinstance(value, int):
        return value
    if hasattr(value, "__int__"):
        return int(value)
    return 0


def __read_cache(scope: InvokeScope) -> CacheDict:
    cache = scope.get(_SCOPE_CACHE)
    if isinstance(cache, dict):
        return cast(CacheDict, cache)
    raise RuntimeError("MCP client cache is required.")


def __add_root_to_sys_path(root_path: str) -> None:
    if root_path and root_path not in sys.path:
        sys.path.insert(0, root_path)


def __is_supported_server(obj: object) -> bool:
    return isinstance(obj, (MCPServer, LowLevelServer))


def __find_server(module: ModuleType) -> PrimitiveServer:
    for obj in vars(module).values():
        if __is_supported_server(obj):
            return obj
    raise RuntimeError("No supported MCP server found in toolset module.")


def __stable_module_name(module_path: str) -> str:
    digest = hashlib.sha256(module_path.encode("utf-8")).hexdigest()[:16]
    return f"rdt_toolset_{digest}"


def __load_server(module_path: str, root_path: str) -> tuple[ModuleType, PrimitiveServer]:
    __add_root_to_sys_path(root_path)
    module_name = __stable_module_name(module_path)
    sys.modules.pop(module_name, None)
    module_spec = importlib.util.spec_from_file_location(module_name, module_path)
    if module_spec is None or module_spec.loader is None:
        raise RuntimeError(f"Cannot load module: {module_path}")
    module = importlib.util.module_from_spec(module_spec)
    sys.modules[module_name] = module
    module_spec.loader.exec_module(module)
    server = __find_server(module)
    return module, server


def __cache_key(module_path: str, mtime_ticks: int) -> tuple[str, int]:
    return (module_path, mtime_ticks)


def __drop_other_mtimes(cache: CacheDict, module_path: str, keep: tuple[str, int]) -> None:
    """Drop older snapshots of the same file. A loop is closed only on its owner thread."""
    thread_id = threading.get_ident()
    for key in list(cache):
        if key[0] != module_path or key == keep:
            continue
        entry = cache.get(key)
        if not isinstance(entry, dict):
            cache.pop(key, None)
            continue
        owner = entry.get("thread_id")
        if owner is not None and owner != thread_id:
            continue
        cache.pop(key, None)
        __dispose_entry(entry)


def __ensure_server_loaded(cache: CacheDict, module_path: str, root_path: str, mtime_ticks: int) -> dict[str, object]:
    key = __cache_key(module_path, mtime_ticks)
    __drop_other_mtimes(cache, module_path, key)
    entry = cache.get(key)
    if entry is not None and entry.get("server") is not None:
        return entry

    module, server = __load_server(module_path, root_path)
    entry = {
        "module": module,
        "server": server,
        "client": None,
        "loop": None,
        "thread_id": None,
    }
    cache[key] = entry
    return entry


async def __enter_client(server: PrimitiveServer) -> Client:
    client = Client(server)
    await client.__aenter__()
    return client


async def __exit_client(client: Client) -> None:
    await client.__aexit__(None, None, None)


def __dispose_entry(entry: dict[str, object]) -> None:
    client = entry.get("client")
    loop = entry.get("loop")
    if isinstance(client, Client) and isinstance(loop, asyncio.AbstractEventLoop):
        try:
            loop.run_until_complete(__exit_client(client))
        except Exception:
            pass
        try:
            loop.close()
        except Exception:
            pass
    entry["client"] = None
    entry["loop"] = None
    entry["thread_id"] = None


def __ensure_client_entered(entry: dict[str, object]) -> tuple[Client, asyncio.AbstractEventLoop]:
    thread_id = threading.get_ident()
    client = entry.get("client")
    loop = entry.get("loop")
    if isinstance(client, Client) and isinstance(loop, asyncio.AbstractEventLoop) and entry.get("thread_id") == thread_id:
        return client, loop

    if client is not None:
        __dispose_entry(entry)

    server = entry.get("server")
    if not __is_supported_server(server):
        raise RuntimeError("Cached MCP server is missing or invalid.")

    loop = asyncio.new_event_loop()
    try:
        entered = loop.run_until_complete(__enter_client(server))
    except Exception:
        loop.close()
        raise

    entry["client"] = entered
    entry["loop"] = loop
    entry["thread_id"] = thread_id
    return entered, loop


def __parse_payload(payload_json: str) -> dict[str, object]:
    payload = json.loads(payload_json) if payload_json else {}
    if not isinstance(payload, dict):
        raise TypeError("Tool payload must be a JSON object.")

    arguments_raw = payload.get("arguments")
    if arguments_raw is None:
        return {}
    if isinstance(arguments_raw, dict):
        return dict(arguments_raw)
    raise TypeError("Tool arguments must be a JSON object.")


def __run_call_tool(
    client: Client,
    loop: asyncio.AbstractEventLoop,
    tool_name: str,
    arguments: dict[str, object],
) -> dict[str, object]:
    async def _call() -> object:
        return await client.session.call_tool(tool_name, arguments)

    result = loop.run_until_complete(_call())
    return __dump_mcp_result(result)


def __run_read_resource(
    client: Client,
    loop: asyncio.AbstractEventLoop,
    resource_uri: str,
) -> dict[str, object]:
    async def _read() -> object:
        return await client.session.read_resource(resource_uri)

    result = loop.run_until_complete(_read())
    return __dump_mcp_result(result)


def __invoke_tool(
    cache: CacheDict,
    module_path: str,
    root_path: str,
    mtime_ticks: int,
    tool_name: str,
    payload_json: str,
) -> str:
    entry = __ensure_server_loaded(cache, module_path, root_path, mtime_ticks)
    client, loop = __ensure_client_entered(entry)
    arguments = __parse_payload(payload_json)
    call_result = __run_call_tool(client, loop, tool_name, arguments)
    return json.dumps(call_result)


def __invoke_resource(
    cache: CacheDict,
    module_path: str,
    root_path: str,
    mtime_ticks: int,
    resource_uri: str,
) -> str:
    entry = __ensure_server_loaded(cache, module_path, root_path, mtime_ticks)
    client, loop = __ensure_client_entered(entry)
    resource_result = __run_read_resource(client, loop, resource_uri)
    return json.dumps(resource_result)


def __clear_client_cache(cache: CacheDict) -> None:
    entries = [cache.pop(key) for key in list(cache)]
    for entry in entries:
        if isinstance(entry, dict):
            __dispose_entry(entry)


def __invoke_from_scope(scope: InvokeScope) -> str:
    operation = __read_scope_string(scope, _SCOPE_OPERATION, _OP_TOOL)
    cache = __read_cache(scope)

    if operation == _OP_CLEAR_CACHE:
        __clear_client_cache(cache)
        return "{}"

    module_path = __read_scope_string(scope, _SCOPE_FILE)
    root_path = __read_scope_string(scope, _SCOPE_ROOT)
    mtime_ticks = __read_scope_int(scope, _SCOPE_MTIME_TICKS)

    if not module_path:
        raise RuntimeError("Tool source file path is required.")

    if operation == _OP_ENSURE_SERVER:
        __ensure_server_loaded(cache, module_path, root_path, mtime_ticks)
        return "{}"

    if operation == _OP_TOOL:
        tool_name = __read_scope_string(scope, _SCOPE_TOOL_NAME)
        if not tool_name:
            raise RuntimeError("Tool name is required.")
        payload_json = __read_scope_string(scope, _SCOPE_PAYLOAD_JSON)
        return __invoke_tool(cache, module_path, root_path, mtime_ticks, tool_name, payload_json)

    if operation == _OP_RESOURCE:
        resource_uri = __read_scope_string(scope, _SCOPE_RESOURCE_URI)
        if not resource_uri:
            raise RuntimeError("Resource URI is required.")
        return __invoke_resource(cache, module_path, root_path, mtime_ticks, resource_uri)

    raise RuntimeError(f"Unsupported invoke operation: {operation}")


__result_json__ = __invoke_from_scope(globals())
