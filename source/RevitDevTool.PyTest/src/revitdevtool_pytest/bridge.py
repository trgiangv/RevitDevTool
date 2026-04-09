"""Named Pipe client for the RevitDevTool bridge.

Protocol: ``[4-byte LE body length][UTF-8 JSON body]``
Matches ``RevitDevTool.McpParser.Models.BridgePipeConnection``.
"""

from __future__ import annotations

import json
import struct
import time
from typing import Any

from .constants import (
    BRIDGE_METHOD_TESTS_EXECUTE,
    BRIDGE_MSG_TYPE_NOTIFICATION,
    DEFAULT_CONNECT_TIMEOUT_MS,
    DEFAULT_TEST_TIMEOUT_S,
)
from .models import BridgeRequest, BridgeResponse, TestOutcome, TestRequest, TestResult

_MAX_FRAME_SIZE = 16 * 1024 * 1024
_HEADER_FMT = "<I"
_HEADER_LEN = struct.calcsize(_HEADER_FMT)


class RevitBridge:
    """Synchronous Named Pipe client for the RevitDevTool bridge."""

    def __init__(self, pipe_name: str, *, connect_timeout_ms: int = DEFAULT_CONNECT_TIMEOUT_MS) -> None:
        self._pipe_name = pipe_name
        self._connect_timeout_ms = connect_timeout_ms
        self._handle: Any = None

    def connect(self) -> None:
        import win32file  # type: ignore[import-untyped]
        import win32pipe  # type: ignore[import-untyped]

        pipe_path = rf"\\.\pipe\{self._pipe_name}"
        deadline = time.monotonic() + self._connect_timeout_ms / 1000.0

        while True:
            try:
                self._handle = win32file.CreateFile(
                    pipe_path,
                    win32file.GENERIC_READ | win32file.GENERIC_WRITE,
                    0, None,
                    win32file.OPEN_EXISTING, 0, None,
                )
                win32pipe.SetNamedPipeHandleState(
                    self._handle, win32pipe.PIPE_READMODE_BYTE, None, None,
                )
                return
            except Exception:
                if time.monotonic() >= deadline:
                    raise ConnectionError(
                        f"Cannot connect to pipe '{self._pipe_name}' "
                        f"within {self._connect_timeout_ms}ms"
                    )
                time.sleep(0.2)

    def disconnect(self) -> None:
        handle, self._handle = self._handle, None
        if handle is None:
            return
        import win32file  # type: ignore[import-untyped]

        try:
            win32file.CloseHandle(handle)
        except Exception:  # noqa: BLE001
            pass

    @property
    def connected(self) -> bool:
        return self._handle is not None

    def execute_test(self, request: TestRequest, *, timeout_s: float = DEFAULT_TEST_TIMEOUT_S) -> TestResult:
        response = self._request(
            BridgeRequest(method=BRIDGE_METHOD_TESTS_EXECUTE, params=request.to_bridge_params()),
            timeout_s,
        )
        if response.is_error:
            return TestResult(outcome=TestOutcome.ERROR, message=response.error_message)
        if isinstance(response.result, dict):
            return TestResult.from_dict(response.result)
        return TestResult(outcome=TestOutcome.ERROR, message=f"Unexpected response: {response.result}")

    def _request(self, req: BridgeRequest, timeout_s: float) -> BridgeResponse:
        self._write_frame(req.to_json_bytes())
        deadline = time.monotonic() + timeout_s
        while True:
            remaining = deadline - time.monotonic()
            if remaining <= 0:
                raise TimeoutError(f"Timed out waiting for response to {req.id}")
            data = self._read_frame(remaining)
            parsed = json.loads(data)
            if parsed.get("type") == BRIDGE_MSG_TYPE_NOTIFICATION:
                continue
            resp = BridgeResponse.from_json(parsed)
            if resp.id == req.id:
                return resp

    def _write_frame(self, body: bytes) -> None:
        import win32file  # type: ignore[import-untyped]

        win32file.WriteFile(self._handle, struct.pack(_HEADER_FMT, len(body)) + body)

    def _read_frame(self, timeout_s: float) -> bytes:
        header = self._read_exact(_HEADER_LEN, timeout_s)
        (body_len,) = struct.unpack(_HEADER_FMT, header)
        if body_len <= 0 or body_len > _MAX_FRAME_SIZE:
            raise RuntimeError(f"Invalid frame length: {body_len}")
        return self._read_exact(body_len, timeout_s)

    def _read_exact(self, count: int, timeout_s: float) -> bytes:
        import win32file  # type: ignore[import-untyped]

        buf = bytearray()
        deadline = time.monotonic() + timeout_s
        while len(buf) < count:
            if time.monotonic() >= deadline:
                raise TimeoutError(f"Timed out reading {count} bytes (got {len(buf)})")
            _, data = win32file.ReadFile(self._handle, count - len(buf))
            if not data:
                raise ConnectionError("Pipe closed while reading")
            buf.extend(data)
        return bytes(buf)
