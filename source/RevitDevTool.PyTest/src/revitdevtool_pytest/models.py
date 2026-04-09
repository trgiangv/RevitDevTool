"""Data models for the RevitDevTool bridge protocol.

Mirrors ``RevitDevTool.McpParser.Models.BridgeMessage`` on the C# side.
"""

from __future__ import annotations

import json
import uuid
from dataclasses import dataclass, field
from enum import Enum
from typing import Any


class TestOutcome(Enum):
    PASSED = "passed"
    FAILED = "failed"
    SKIPPED = "skipped"
    ERROR = "error"


@dataclass(frozen=True, slots=True)
class TestRequest:
    module_source: str
    test_name: str
    file_path: str
    class_name: str | None = None

    def to_bridge_params(self) -> dict[str, Any]:
        params: dict[str, Any] = {
            "module_source": self.module_source,
            "test_name": self.test_name,
            "file_path": self.file_path,
        }
        if self.class_name:
            params["class_name"] = self.class_name
        return params


@dataclass(frozen=True, slots=True)
class TestResult:
    outcome: TestOutcome = TestOutcome.ERROR
    message: str = ""
    traceback: str = ""
    stdout: str = ""
    duration_ms: float = 0.0

    @classmethod
    def from_dict(cls, data: dict[str, Any]) -> TestResult:
        raw = data.get("outcome", "error")
        try:
            outcome = TestOutcome(raw)
        except ValueError:
            outcome = TestOutcome.ERROR
        return cls(
            outcome=outcome,
            message=data.get("message", ""),
            traceback=data.get("traceback", ""),
            stdout=data.get("stdout", ""),
            duration_ms=data.get("duration_ms", 0.0),
        )


@dataclass(slots=True)
class BridgeRequest:
    method: str
    params: dict[str, Any] | None = None
    id: str = field(default_factory=lambda: str(uuid.uuid4()))

    def to_json_bytes(self) -> bytes:
        msg: dict[str, Any] = {
            "type": "request",
            "id": self.id,
            "method": self.method,
        }
        if self.params is not None:
            msg["params"] = self.params
        return json.dumps(msg, ensure_ascii=False).encode("utf-8")


@dataclass(frozen=True, slots=True)
class BridgeResponse:
    id: str
    result: Any = None
    is_error: bool = False
    error_message: str = ""

    @classmethod
    def from_json(cls, data: dict[str, Any]) -> BridgeResponse:
        return cls(
            id=data.get("id", ""),
            result=data.get("result"),
            is_error=data.get("isError", False),
            error_message=data.get("errorMessage", ""),
        )
