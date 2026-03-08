from __future__ import annotations

import argparse
import json

try:
    from .app import RevitBridge, RevitBridgeError
except ImportError:
    from app import RevitBridge, RevitBridgeError


def run_attach_and_list(port: int, timeout: float, retry_delay: float) -> int:
    bridge = RevitBridge(port, timeout, retry_delay)
    try:
        bridge.connect()
        tools = bridge.list_tools()
        payload = {"ok": True, "toolCount": len(tools), "tools": [tool.name for tool in tools]}
        print(json.dumps(payload))
        return 0
    except RevitBridgeError as exc:
        print(json.dumps({"ok": False, "stage": "attach_list", "error": str(exc)}))
        return 2
    finally:
        bridge.close()


def run_list_and_call_python_tool(
    port: int,
    timeout: float,
    retry_delay: float,
    tool_name: str,
    call_name: str,
) -> int:
    bridge = RevitBridge(port, timeout, retry_delay)
    try:
        bridge.connect()
        tools = bridge.list_tools()
        matching = next((tool for tool in tools if tool.name == tool_name), None)
        if matching is None:
            print(
                json.dumps(
                    {
                        "ok": False,
                        "stage": "tools.list",
                        "error": f"Tool '{tool_name}' was not found.",
                        "tools": [tool.name for tool in tools],
                    }
                )
            )
            return 6

        result = bridge.call_tool(tool_name, matching.toolId, {"name": call_name})
        payload = result.get("payload")
        if payload is None:
            print(
                json.dumps(
                    {
                        "ok": False,
                        "stage": "tool.call",
                        "error": "Unexpected Python tool result.",
                        "rawResult": result,
                    }
                )
            )
            return 7

        print(
            json.dumps(
                {
                    "ok": True,
                    "tool": tool_name,
                    "toolCount": len(tools),
                    "result": payload,
                }
            )
        )
        return 0
    except RevitBridgeError as exc:
        print(json.dumps({"ok": False, "stage": "python_tool_call", "error": str(exc)}))
        return 8
    finally:
        bridge.close()
def main() -> int:
    parser = argparse.ArgumentParser(description="MCP bridge smoke tests")
    parser.add_argument("--port", required=True, type=int)
    parser.add_argument("--timeout", type=float, default=12.0)
    parser.add_argument("--retry-delay", type=float, default=0.4)
    parser.add_argument(
        "--mode",
        choices=["attach-list", "python-tool-call"],
        default="attach-list",
    )
    parser.add_argument("--tool-name", default="rdt_builtin_ping")
    parser.add_argument("--name", default="SmokeTest")
    args = parser.parse_args()

    if args.mode == "attach-list":
        return run_attach_and_list(args.port, args.timeout, args.retry_delay)
    return run_list_and_call_python_tool(args.port, args.timeout, args.retry_delay, args.tool_name, args.name)


if __name__ == "__main__":
    raise SystemExit(main())
