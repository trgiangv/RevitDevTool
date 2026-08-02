import anyio
import importlib.util
import json
import sys
import uuid
from types import ModuleType
from typing import Any, Mapping, TypeAlias, cast

from pydantic import BaseModel
from mcp import types
from mcp.server.context import ServerRequestContext
from mcp.server.lowlevel import Server as LowLevelServer
from mcp.server.mcpserver import MCPServer

JsonScalar: TypeAlias = str | int | float | bool | None
JsonValue: TypeAlias = JsonScalar | dict[str, "JsonValue"] | list["JsonValue"]
PrimitiveServer: TypeAlias = MCPServer | LowLevelServer[Any]
InvokeScope: TypeAlias = Mapping[str, object]

_SCOPE_FILE = "__file__"
_SCOPE_ROOT = "__root__"
_SCOPE_SOURCE_FILE = "__source_file__"
_SCOPE_OPERATION = "__operation__"
_SCOPE_TOOL_NAME = "__tool_name__"
_SCOPE_PAYLOAD_JSON = "__payload_json__"
_SCOPE_RESOURCE_URI = "__resource_uri__"

_OP_TOOL = "tool"
_OP_RESOURCE = "resource"

_LOWLEVEL_METHOD_CALL_TOOL = "tools/call"
_LOWLEVEL_METHOD_READ_RESOURCE = "resources/read"


def __normalize(value: object) -> JsonValue:
    if value is None or isinstance(value, (str, int, float, bool)):
        return value
    if isinstance(value, bytes):
        return value.decode("utf-8", errors="replace")
    if isinstance(value, BaseModel):
        return __normalize(value.model_dump(by_alias=True, exclude_none=True))
    if isinstance(value, dict):
        return {str(key): __normalize(item) for key, item in value.items()}
    if isinstance(value, (list, tuple, set)):
        return [__normalize(item) for item in value]
    return str(value)


def __read_scope_string(scope: InvokeScope, key: str, default: str = "") -> str:
    value = scope.get(key, default)
    return value if isinstance(value, str) else default


def __parse_payload(payload_json: str) -> dict[str, object]:
    payload = json.loads(payload_json) if payload_json else {}
    if not isinstance(payload, dict):
        raise RuntimeError("Tool payload must be a JSON object.")

    return payload


def __add_root_to_sys_path(root_path: str) -> None:
    if root_path and root_path not in sys.path:
        sys.path.insert(0, root_path)


def __is_supported_server(obj: object) -> bool:
    return isinstance(obj, (MCPServer, LowLevelServer))


def __find_server(module: ModuleType) -> PrimitiveServer | None:
    for obj in vars(module).values():
        if __is_supported_server(obj):
            return obj
    return None


def __load_module(module_path: str) -> ModuleType:
    module_name = f"rdt_invoke_{uuid.uuid4().hex}"
    module_spec = importlib.util.spec_from_file_location(module_name, module_path)
    if module_spec is None or module_spec.loader is None:
        raise RuntimeError(f"Cannot load module: {module_path}")

    module = importlib.util.module_from_spec(module_spec)
    module_spec.loader.exec_module(module)
    return module


def __make_lowlevel_context(method: str, protocol_version: str = "2025-11-25") -> ServerRequestContext[Any]:
    return ServerRequestContext(
        session=cast(Any, None),
        lifespan_context={},
        protocol_version=protocol_version,
        method=method,
    )


def __build_call_tool_params(tool_name: str, payload: dict[str, object]) -> types.CallToolRequestParams:
    input_responses_raw = payload.get("inputResponses")
    if input_responses_raw is None:
        input_responses_raw = payload.get("input_responses")
    request_state_raw = payload.get("requestState")
    if request_state_raw is None:
        request_state_raw = payload.get("request_state")
    has_mrtr = input_responses_raw is not None or request_state_raw is not None

    if not has_mrtr:
        return types.CallToolRequestParams(name=tool_name, arguments=dict(payload))

    arguments_raw = payload.get("arguments")
    arguments = dict(arguments_raw) if isinstance(arguments_raw, dict) else {}
    kwargs: dict[str, object] = {"name": tool_name, "arguments": arguments}
    if input_responses_raw is not None:
        kwargs["input_responses"] = input_responses_raw
    if isinstance(request_state_raw, str):
        kwargs["request_state"] = request_state_raw
    return types.CallToolRequestParams.model_validate(kwargs)


def __invoke_mcpserver(server: MCPServer, params: types.CallToolRequestParams, has_mrtr: bool) -> object:
    if has_mrtr:
        entry = server._lowlevel_server.get_request_handler(_LOWLEVEL_METHOD_CALL_TOOL)
        if entry is None:
            raise RuntimeError("MCPServer does not register a tools/call handler.")
        ctx = __make_lowlevel_context(_LOWLEVEL_METHOD_CALL_TOOL, "2026-07-28")
        return anyio.run(entry.handler, ctx, params)

    return anyio.run(server.call_tool, params.name, params.arguments or {})


def __invoke_lowlevel(
    server: LowLevelServer[Any],
    params: types.CallToolRequestParams,
    has_mrtr: bool,
) -> object:
    entry = server.get_request_handler(_LOWLEVEL_METHOD_CALL_TOOL)
    if entry is None:
        raise RuntimeError("Low-level MCP server does not register a tools/call handler.")

    protocol_version = "2026-07-28" if has_mrtr else "2025-11-25"
    ctx = __make_lowlevel_context(_LOWLEVEL_METHOD_CALL_TOOL, protocol_version)
    return anyio.run(entry.handler, ctx, params)


def __invoke_server(server: PrimitiveServer, tool_name: str, payload: dict[str, object]) -> object:
    params = __build_call_tool_params(tool_name, payload)
    has_mrtr = params.input_responses is not None or params.request_state is not None
    if isinstance(server, MCPServer):
        return __invoke_mcpserver(server, params, has_mrtr)
    if isinstance(server, LowLevelServer):
        return __invoke_lowlevel(server, params, has_mrtr)

    raise RuntimeError(f"Unsupported MCP server type: {type(server)!r}")


def __invoke_mcpserver_resource(server: MCPServer, uri: str) -> object:
    contents = anyio.run(server.read_resource, uri)
    return {"contents": __normalize(list(contents))}


def __invoke_lowlevel_resource(server: LowLevelServer[Any], uri: str) -> object:
    entry = server.get_request_handler(_LOWLEVEL_METHOD_READ_RESOURCE)
    if entry is None:
        raise RuntimeError("Low-level MCP server does not register a resources/read handler.")

    params = types.ReadResourceRequestParams(uri=uri)
    ctx = __make_lowlevel_context(_LOWLEVEL_METHOD_READ_RESOURCE)
    return anyio.run(entry.handler, ctx, params)


def __invoke_resource_server(server: PrimitiveServer, uri: str) -> object:
    if isinstance(server, MCPServer):
        return __invoke_mcpserver_resource(server, uri)
    if isinstance(server, LowLevelServer):
        return __invoke_lowlevel_resource(server, uri)

    raise RuntimeError(f"Unsupported MCP server type: {type(server)!r}")


def __invoke_tool(
    module_path: str,
    root_path: str,
    source_file: str,
    tool_name: str,
    payload_json: str,
) -> str:
    payload = __parse_payload(payload_json)
    __add_root_to_sys_path(root_path)
    module = __load_module(module_path)
    server = __find_server(module)
    if server is None:
        raise RuntimeError(f"No supported MCP server found in '{source_file}'.")

    call_result = __invoke_server(server, tool_name, payload)
    return json.dumps(__normalize(call_result))


def __invoke_resource(
    module_path: str,
    root_path: str,
    source_file: str,
    resource_uri: str,
) -> str:
    __add_root_to_sys_path(root_path)
    module = __load_module(module_path)
    server = __find_server(module)
    if server is None:
        raise RuntimeError(f"No supported MCP server found in '{source_file}'.")

    resource_result = __invoke_resource_server(server, resource_uri)
    return json.dumps(__normalize(resource_result))


def __invoke_from_scope(scope: InvokeScope) -> str:
    module_path = __read_scope_string(scope, _SCOPE_FILE)
    root_path = __read_scope_string(scope, _SCOPE_ROOT)
    source_file = __read_scope_string(scope, _SCOPE_SOURCE_FILE, module_path)
    operation = __read_scope_string(scope, _SCOPE_OPERATION, _OP_TOOL)
    tool_name = __read_scope_string(scope, _SCOPE_TOOL_NAME)
    payload_json = __read_scope_string(scope, _SCOPE_PAYLOAD_JSON)
    resource_uri = __read_scope_string(scope, _SCOPE_RESOURCE_URI)

    if not module_path:
        raise RuntimeError("Tool source file path is required.")
    if operation == _OP_TOOL:
        if not tool_name:
            raise RuntimeError("Tool name is required.")
        return __invoke_tool(module_path, root_path, source_file, tool_name, payload_json)
    if operation == _OP_RESOURCE:
        if not resource_uri:
            raise RuntimeError("Resource URI is required.")
        return __invoke_resource(module_path, root_path, source_file, resource_uri)

    raise RuntimeError(f"Unsupported invoke operation: {operation}")


__result_json__ = __invoke_from_scope(globals())
