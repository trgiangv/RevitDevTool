"""RevitDevTool standalone MCP server.

Launched by MCP hosts (Cursor, Claude Desktop, ...), connects to Revit TCP
bridge on localhost, fetches registered tools, and forwards tool calls.

Usage:
    python app.py --port 18080
"""

from __future__ import annotations

import argparse
import asyncio
import json
import socket
import struct
import time
import uuid
from typing import Any

from mcp.server.fastmcp import FastMCP
from mcp.types import ToolAnnotations
from pydantic import ValidationError

try:
    from .schemas import (
        BridgeEnvelope,
        ToolListPayload,
        PROTOCOL_VERSION,
        SCHEMA_VERSION,
        SOURCE_CHECKSUM,
        ToolCallPayload,
        ToolDefinition,
    )
except ImportError:
    from schemas import (
        BridgeEnvelope,
        ToolListPayload,
        PROTOCOL_VERSION,
        SCHEMA_VERSION,
        SOURCE_CHECKSUM,
        ToolCallPayload,
        ToolDefinition,
    )

_LIST_TOOLS = "tools.list"
_TOOL_CALL = "tool.call"
_KIND_REQUEST = "request"


class RevitBridgeError(RuntimeError):
    pass


class RevitBridge:
    """TCP bridge using length-prefixed JSON payloads."""

    def __init__(
        self,
        port: int,
        connect_timeout: float,
        connect_retry_delay: float,
    ) -> None:
        self._host = "127.0.0.1"
        self._port = port
        self._connect_timeout = connect_timeout
        self._connect_retry_delay = connect_retry_delay
        self._socket: socket.socket | None = None

    def connect(self) -> None:
        deadline = time.monotonic() + self._connect_timeout
        last_error: Exception | None = None

        while time.monotonic() < deadline:
            try:
                self._socket = socket.create_connection((self._host, self._port), timeout=self._connect_timeout)
                self._socket.settimeout(self._connect_timeout)
                return
            except OSError as exc:
                last_error = exc
                time.sleep(self._connect_retry_delay)

        hint = (
            f"Unable to connect to {self._host}:{self._port} within {self._connect_timeout:.1f}s. "
            "Ensure RevitDevTool TCP bridge is running."
        )
        raise RevitBridgeError(f"{hint} Last error: {last_error}") from last_error

    def close(self) -> None:
        if self._socket is not None:
            try:
                self._socket.close()
            finally:
                self._socket = None

    def _make_envelope(self, action: str, **extra: Any) -> dict[str, Any]:
        envelope = {
            "id": uuid.uuid4().hex,
            "version": PROTOCOL_VERSION,
            "schemaVersion": SCHEMA_VERSION,
            "schemaChecksum": SOURCE_CHECKSUM,
            "kind": _KIND_REQUEST,
            "action": action,
            "payloadJson": "{}",
            **extra,
        }
        return BridgeEnvelope.model_validate(envelope).model_dump(exclude_none=True)

    def _write_message(self, envelope: dict[str, Any]) -> None:
        payload = json.dumps(envelope).encode("utf-8")
        header = struct.pack("<I", len(payload))
        if self._socket is None:
            raise RevitBridgeError("Bridge socket is not connected.")
        self._socket.sendall(header + payload)

    def _read_exact(self, size: int) -> bytes:
        chunks: list[bytes] = []
        remaining = size
        while remaining > 0:
            if self._socket is None:
                raise RevitBridgeError("Bridge socket is not connected.")
            chunk = self._socket.recv(remaining)
            if not chunk:
                raise RevitBridgeError("TCP socket closed while reading response.")
            chunks.append(chunk)
            remaining -= len(chunk)
        return b"".join(chunks)

    def _read_message(self) -> BridgeEnvelope:
        raw_len = self._read_exact(4)
        (length,) = struct.unpack("<I", raw_len)
        raw_payload = self._read_exact(length)
        try:
            decoded = json.loads(raw_payload.decode("utf-8"))
            return BridgeEnvelope.model_validate(decoded)
        except (json.JSONDecodeError, ValidationError) as exc:
            raise RevitBridgeError(f"Invalid bridge response envelope: {exc}") from exc

    def _send(self, envelope: dict[str, Any]) -> BridgeEnvelope:
        if self._socket is None:
            raise RevitBridgeError("Bridge is not connected.")
        self._write_message(envelope)
        return self._read_message()

    def list_tools(self) -> list[ToolDefinition]:
        response = self._send(self._make_envelope(_LIST_TOOLS))
        if response.error:
            raise RevitBridgeError(f"tools.list failed: [{response.error.code}] {response.error.message}")
        try:
            payload = ToolListPayload.model_validate_json(response.payloadJson)
        except ValidationError as exc:
            raise RevitBridgeError(f"Invalid tools.list payload schema: {exc}") from exc
        return payload.tools

    def call_tool(self, tool_name: str, tool_id: str | None, arguments: dict[str, Any]) -> dict[str, Any]:
        execution_id = uuid.uuid4().hex
        response = self._send(
            self._make_envelope(
                _TOOL_CALL,
                executionId=execution_id,
                toolId=tool_id,
                toolName=tool_name,
                payloadJson=json.dumps(arguments),
            )
        )
        if response.error:
            raise RevitBridgeError(f"[{response.error.code}] {response.error.message}")
        try:
            payload = json.loads(response.payloadJson)
        except json.JSONDecodeError:
            raise RevitBridgeError("Invalid tool.call payloadJson: expected valid JSON object/value.")

        try:
            strict_payload = ToolCallPayload.model_validate(
                {
                    "payload": payload,
                    "message": response.message or "",
                    "resultKind": response.resultKind or _infer_result_kind(payload),
                    "metadata": response.metadata or {},
                    "progressUpdates": response.progressUpdates or [],
                }
            )
        except ValidationError as exc:
            raise RevitBridgeError(f"Invalid tool.call response schema: {exc}") from exc

        return strict_payload.model_dump()


def _infer_result_kind(payload: Any) -> str:
    if payload is None:
        return "empty"
    if isinstance(payload, str):
        return "text"
    return "json"


def _unwrap_tool_result(result: dict[str, Any]) -> Any:
    return result.get("payload")


def _parse_annotations(tool: ToolDefinition) -> ToolAnnotations | None:
    if tool.annotationsJson:
        return ToolAnnotations.model_validate_json(tool.annotationsJson)
    return None


def _parse_meta(tool: ToolDefinition) -> dict[str, Any] | None:
    if tool.metaJson:
        loaded = json.loads(tool.metaJson)
        if not isinstance(loaded, dict):
            raise RevitBridgeError(f"Invalid metaJson for tool '{tool.name}': expected object.")
        return loaded
    return None


def _parse_output_schema(tool: ToolDefinition) -> dict[str, Any] | None:
    if not tool.outputSchemaJson:
        return None
    loaded = json.loads(tool.outputSchemaJson)
    if not isinstance(loaded, dict):
        raise RevitBridgeError(f"Invalid outputSchemaJson for tool '{tool.name}': expected object.")
    return loaded


def build_server(bridge: RevitBridge, tools: list[ToolDefinition]) -> FastMCP:
    server = FastMCP("revitdevtool-mcp-server", json_response=True)

    def create_handler(tool_name: str, tool_id: str | None, tool_description: str):
        async def handler(**kwargs: Any) -> dict[str, Any]:
            result = await asyncio.to_thread(bridge.call_tool, tool_name, tool_id, kwargs)
            return _unwrap_tool_result(result)

        handler.__name__ = tool_name
        handler.__doc__ = tool_description
        handler.__annotations__["return"] = dict[str, Any]
        return handler

    for tool in tools:
        name = tool.name
        description = (tool.description or "").strip() or "No description provided."
        annotations = _parse_annotations(tool)
        meta = _parse_meta(tool)
        output_schema = _parse_output_schema(tool)

        handler = create_handler(name, tool.toolId, description)
        server.add_tool(
            handler,
            name=name,
            description=description,
            annotations=annotations,
            meta=meta,
            structured_output=tool.structuredOutput,
        )
        registered_tool = server._tool_manager._tools[name]
        if output_schema is not None:
            registered_tool.output_schema = output_schema
    return server


async def run(
    port: int,
    connect_timeout: float,
    connect_retry_delay: float,
) -> None:
    bridge = RevitBridge(port, connect_timeout, connect_retry_delay)
    await asyncio.to_thread(bridge.connect)
    tools = await asyncio.to_thread(bridge.list_tools)

    server = build_server(bridge, tools)
    try:
        await server.run_stdio_async()
    finally:
        await asyncio.to_thread(bridge.close)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("port_positional", type=int, nargs="?", help="Revit TCP port (positional)")
    parser.add_argument("--port", type=int, required=False, help="Revit TCP port")
    parser.add_argument("--connect-timeout", type=float, default=20.0, help="TCP connect timeout in seconds")
    parser.add_argument("--connect-retry-delay", type=float, default=0.4, help="Delay between retries in seconds")
    args = parser.parse_args()
    port = args.port if args.port is not None else args.port_positional
    if port is None:
        parser.error("A Revit TCP port is required. Pass --port <port> or a positional port value.")
    asyncio.run(run(port, args.connect_timeout, args.connect_retry_delay))


if __name__ == "__main__":
    main()
