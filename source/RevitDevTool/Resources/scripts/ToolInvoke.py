import anyio
import importlib.util
import json
import sys
import uuid
from types import ModuleType
from typing import Any, Mapping, TypeAlias

from pydantic import BaseModel
from mcp import types
from mcp.server.fastmcp import FastMCP
from mcp.server.lowlevel import Server as LowLevelServer

JsonScalar: TypeAlias = str | int | float | bool | None
JsonValue: TypeAlias = JsonScalar | dict[str, "JsonValue"] | list["JsonValue"]
PrimitiveServer: TypeAlias = FastMCP | LowLevelServer[Any]
InvokeScope: TypeAlias = Mapping[str, object]


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


def __parse_optional_payload(payload_json: str) -> dict[str, object] | None:
    if not payload_json:
        return None

    payload = json.loads(payload_json)
    if payload is None:
        return None
    if not isinstance(payload, dict):
        raise RuntimeError("Primitive payload must be a JSON object.")

    return payload


def __add_root_to_sys_path(root_path: str) -> None:
    if root_path and root_path not in sys.path:
        sys.path.insert(0, root_path)


def __is_supported_server(obj: object) -> bool:
    return isinstance(obj, (FastMCP, LowLevelServer))


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


def __invoke_fastmcp(server: FastMCP, tool_name: str, payload: dict[str, object]) -> object:
    return anyio.run(server.call_tool, tool_name, payload)


def __invoke_lowlevel(
    server: LowLevelServer[Any],
    tool_name: str,
    payload: dict[str, object],
) -> object:
    handler = server.request_handlers.get(types.CallToolRequest)
    if handler is None:
        raise RuntimeError("Low-level MCP server does not register a tools/call handler.")

    request = types.CallToolRequest(
        params=types.CallToolRequestParams(name=tool_name, arguments=payload)
    )
    result = anyio.run(handler, request)
    return result.root if result is not None else None


def __invoke_server(server: PrimitiveServer, tool_name: str, payload: dict[str, object]) -> object:
    if isinstance(server, FastMCP):
        return __invoke_fastmcp(server, tool_name, payload)
    if isinstance(server, LowLevelServer):
        return __invoke_lowlevel(server, tool_name, payload)

    raise RuntimeError(f"Unsupported MCP server type: {type(server)!r}")


def __invoke_fastmcp_prompt(server: FastMCP, prompt_name: str, arguments: dict[str, object] | None) -> object:
    return anyio.run(server.get_prompt, prompt_name, arguments)


def __invoke_lowlevel_prompt(
    server: LowLevelServer[Any],
    prompt_name: str,
    arguments: dict[str, object] | None,
) -> object:
    handler = server.request_handlers.get(types.GetPromptRequest)
    if handler is None:
        raise RuntimeError("Low-level MCP server does not register a prompts/get handler.")

    request = types.GetPromptRequest(
        params=types.GetPromptRequestParams(name=prompt_name, arguments=arguments)
    )
    result = anyio.run(handler, request)
    return result.root if result is not None else None


def __invoke_prompt_server(
    server: PrimitiveServer,
    prompt_name: str,
    arguments: dict[str, object] | None,
) -> object:
    if isinstance(server, FastMCP):
        return __invoke_fastmcp_prompt(server, prompt_name, arguments)
    if isinstance(server, LowLevelServer):
        return __invoke_lowlevel_prompt(server, prompt_name, arguments)

    raise RuntimeError(f"Unsupported MCP server type: {type(server)!r}")


def __invoke_fastmcp_resource(server: FastMCP, uri: str) -> object:
    contents = anyio.run(server.read_resource, uri)
    return {"contents": __normalize(list(contents))}


def __invoke_lowlevel_resource(server: LowLevelServer[Any], uri: str) -> object:
    handler = server.request_handlers.get(types.ReadResourceRequest)
    if handler is None:
        raise RuntimeError("Low-level MCP server does not register a resources/read handler.")

    request = types.ReadResourceRequest(
        params=types.ReadResourceRequestParams(uri=uri)
    )
    result = anyio.run(handler, request)
    return result.root if result is not None else None


def __invoke_resource_server(server: PrimitiveServer, uri: str) -> object:
    if isinstance(server, FastMCP):
        return __invoke_fastmcp_resource(server, uri)
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


def __invoke_prompt(
    module_path: str,
    root_path: str,
    source_file: str,
    prompt_name: str,
    arguments_json: str,
) -> str:
    arguments = __parse_optional_payload(arguments_json)
    __add_root_to_sys_path(root_path)
    module = __load_module(module_path)
    server = __find_server(module)
    if server is None:
        raise RuntimeError(f"No supported MCP server found in '{source_file}'.")

    prompt_result = __invoke_prompt_server(server, prompt_name, arguments)
    return json.dumps(__normalize(prompt_result))


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
    module_path = __read_scope_string(scope, "__file__")
    root_path = __read_scope_string(scope, "__root__")
    source_file = __read_scope_string(scope, "__source_file__", module_path)
    operation = __read_scope_string(scope, "__operation__", "tool")
    tool_name = __read_scope_string(scope, "__tool_name__")
    payload_json = __read_scope_string(scope, "__payload_json__")
    prompt_name = __read_scope_string(scope, "__prompt_name__")
    arguments_json = __read_scope_string(scope, "__arguments_json__")
    resource_uri = __read_scope_string(scope, "__resource_uri__")

    if not module_path:
        raise RuntimeError("Tool source file path is required.")
    if operation == "tool":
        if not tool_name:
            raise RuntimeError("Tool name is required.")
        return __invoke_tool(module_path, root_path, source_file, tool_name, payload_json)
    if operation == "prompt":
        if not prompt_name:
            raise RuntimeError("Prompt name is required.")
        return __invoke_prompt(module_path, root_path, source_file, prompt_name, arguments_json)
    if operation == "resource":
        if not resource_uri:
            raise RuntimeError("Resource URI is required.")
        return __invoke_resource(module_path, root_path, source_file, resource_uri)

    raise RuntimeError(f"Unsupported invoke operation: {operation}")


__result_json__ = __invoke_from_scope(globals())