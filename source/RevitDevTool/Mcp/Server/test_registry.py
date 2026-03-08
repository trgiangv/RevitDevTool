from __future__ import annotations

import unittest
from pathlib import Path

try:
    from .schemas.models import BridgeEnvelope, ToolCallPayload
    from .tools_parser import parse_path
except ImportError:
    from schemas.models import BridgeEnvelope, ToolCallPayload
    from tools_parser import parse_path


class ParserTests(unittest.TestCase):
    def test_parse_returns_list(self):
        # parser on an empty/non-existent dir returns empty list gracefully
        result = parse_path(Path("."))
        self.assertIsInstance(result, list)

    def test_tool_definition_shape(self):
        # each item must have the fields C# McpToolDefinition expects
        result = parse_path(Path("."))
        for tool in result:
            self.assertIn("name", tool)
            self.assertIn("description", tool)
            self.assertIn("inputSchemaJson", tool)

    def test_bridge_envelope_strict_rejects_unknown_field(self):
        with self.assertRaises(Exception):
            BridgeEnvelope.model_validate(
                {
                    "id": "1",
                    "version": "1.0",
                    "schemaVersion": "2026-03-08",
                    "schemaChecksum": "x",
                    "kind": "request",
                    "action": "tools.list",
                    "payloadJson": "{}",
                    "unexpectedField": "not-allowed",
                }
            )

    def test_tool_call_payload_requires_str_result_kind(self):
        with self.assertRaises(Exception):
            ToolCallPayload.model_validate(
                {
                    "payload": {"ok": True},
                    "message": "done",
                    "resultKind": 5,
                    "metadata": {},
                    "progressUpdates": [],
                }
            )


if __name__ == "__main__":
    unittest.main()
