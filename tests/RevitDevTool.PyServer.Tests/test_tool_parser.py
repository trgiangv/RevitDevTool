from typing import Any

import pytest


def _find_by_name(entries: list[dict[str, Any]], name: str) -> dict[str, Any]:
    return next(e for e in entries if e["protocol"]["name"] == name)


def test_mcpserver_tool_protocol_shape(parsed_catalog: dict[str, list[dict[str, Any]]]) -> None:
    entry = _find_by_name(parsed_catalog["tools"], "get_parser_sample_status")
    protocol = entry["protocol"]

    assert "title" not in protocol, "MCPServer tools set title in annotations, not at top level"
    assert protocol["annotations"]["title"] == "Get Parser Sample Status"
    assert protocol["annotations"]["readOnlyHint"] is True
    assert protocol["annotations"]["idempotentHint"] is True
    assert protocol["annotations"]["openWorldHint"] is False

    assert isinstance(protocol["inputSchema"], dict)
    assert protocol["inputSchema"]["type"] == "object"

    assert protocol["outputSchema"]["properties"]["status"]["type"] == "string"

    assert protocol["icons"][0]["src"] == "https://example.com/icons/tool.png"
    assert protocol["_meta"]["feature"] == "mcpserver"
    assert protocol["_meta"]["version"] == 2


def test_mcpserver_tool_binding(parsed_catalog: dict[str, list[dict[str, Any]]]) -> None:
    entry = _find_by_name(parsed_catalog["tools"], "get_parser_sample_status")
    binding = entry["binding"]

    assert binding["methodName"] == "get_parser_sample_status"
    assert binding["sourcePath"].endswith(".py")
    assert binding["containerType"] != ""


def test_lowlevel_tool_protocol_shape(parsed_catalog: dict[str, list[dict[str, Any]]]) -> None:
    entry = _find_by_name(parsed_catalog["tools"], "parser_lowlevel_tool")
    protocol = entry["protocol"]

    assert protocol["title"] == "Parser Low-Level Tool"
    assert protocol["annotations"]["readOnlyHint"] is True
    assert protocol["annotations"]["idempotentHint"] is True

    assert protocol["outputSchema"]["properties"]["status"]["type"] == "string"
    assert protocol["_meta"]["feature"] == "lowlevel"


def test_prompt_protocol_shape(parsed_catalog: dict[str, list[dict[str, Any]]]) -> None:
    entry = _find_by_name(parsed_catalog["prompts"], "parser_lowlevel_prompt")
    protocol = entry["protocol"]

    assert protocol["icons"][0]["src"] == "https://example.com/icons/lowlevel-prompt.png"
    assert protocol["_meta"]["kind"] == "prompt"


def test_resource_protocol_shape(parsed_catalog: dict[str, list[dict[str, Any]]]) -> None:
    direct = _find_by_name(parsed_catalog["resources"], "parser_lowlevel_resource")
    template = _find_by_name(parsed_catalog["resources"], "parser_lowlevel_template")

    assert direct["isTemplate"] is False
    assert direct["protocol"]["uri"] == "sample://lowlevel/status"
    assert direct["protocol"]["mimeType"] == "text/plain"
    assert direct["protocol"]["size"] == 128
    assert direct["protocol"]["_meta"]["kind"] == "resource"
    assert direct["protocol"]["annotations"]["priority"] == pytest.approx(0.8)

    assert template["isTemplate"] is True
    assert template["protocol"]["uriTemplate"] == "sample://lowlevel/items/{item_id}"
    assert template["protocol"]["mimeType"] == "application/json"
    assert template["protocol"]["_meta"]["kind"] == "template"
    assert template["protocol"]["annotations"]["priority"] == pytest.approx(0.5)
