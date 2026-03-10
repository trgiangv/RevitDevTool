import anyio
import contextlib
import importlib.util
import inspect
import json
import sys
import uuid

def __normalize(value):
    if value is None or isinstance(value, (str, int, float, bool)):
        return value
    if isinstance(value, bytes):
        return value.decode("utf-8", errors="replace")
    if hasattr(value, "model_dump"):
        return __normalize(value.model_dump(by_alias=True, exclude_none=True))
    if hasattr(value, "dict") and callable(getattr(value, "dict")):
        return __normalize(value.dict())
    if isinstance(value, dict):
        return {str(key): __normalize(item) for key, item in value.items()}
    if isinstance(value, (list, tuple, set)):
        return [__normalize(item) for item in value]
    for attr_name in ("text", "content", "data", "result"):
        attr_value = getattr(value, attr_name, None)
        if attr_value is not None:
            return __normalize(attr_value)
    return str(value)

def __looks_like_mcp(obj):
    return obj is not None and not inspect.isclass(obj) and callable(getattr(obj, "list_tools", None))

def __find_server(module):
    get_fn = getattr(module, "get_mcp_server", None)
    if callable(get_fn):
        with contextlib.suppress(Exception):
            c = get_fn()
            if __looks_like_mcp(c):
                return c
    mcp_attr = getattr(module, "mcp", None)
    if __looks_like_mcp(mcp_attr):
        return mcp_attr
    for _name, obj in inspect.getmembers(module):
        if __looks_like_mcp(obj):
            return obj
    return None

__payload = json.loads(__payload_json__) if __payload_json__ else {}
if not isinstance(__payload, dict):
    raise RuntimeError("Tool payload must be a JSON object.")

if __root__ and __root__ not in sys.path:
    sys.path.insert(0, __root__)

__mod_name = f"rdt_invoke_{uuid.uuid4().hex}"
__spec = importlib.util.spec_from_file_location(__mod_name, __file__)
__mod = importlib.util.module_from_spec(__spec)
__spec.loader.exec_module(__mod)

__server = __find_server(__mod)
if __server is None:
    raise RuntimeError(f"No FastMCP server found in '{__source_file}'.")

__tool = __server._tool_manager.get_tool(__tool_name__)
if __tool is None:
    raise RuntimeError(f"MCP tool '{__tool_name__}' was not found in server.")

__call_result = anyio.run(__tool.run, __payload, None, False)
__result_json__ = json.dumps(__normalize(__call_result))