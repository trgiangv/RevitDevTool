"""Typed message router for the WebView2 ↔ Python bridge.

Incoming ``postMessage`` calls from the React frontend are routed to
registered handlers.  Responses are emitted back as ``CustomEvent`` via
``ExecuteScriptAsync``.

The ``emit`` helper injects JSON safely using ``JSON.parse`` on the JS side
— no fragile double-escaping.
"""

from __future__ import annotations

import json
from collections.abc import Callable
from typing import TYPE_CHECKING, Any

if TYPE_CHECKING:
    from Microsoft.Web.WebView2.Wpf import WebView2


class MessageRouter:
    """Registry-based message dispatcher for WebView2."""

    def __init__(
        self,
        webview: WebView2,
        handlers: dict[str, Callable[[dict], None]],
    ) -> None:
        self._webview = webview
        self._handlers = handlers

    # -- inbound (JS → Python) ----------------------------------------------

    def handle_raw(self, raw_json: str) -> None:
        """Parse a raw JSON string from ``WebMessageReceived`` and dispatch."""
        try:
            message: dict[str, Any] = json.loads(raw_json)
        except (json.JSONDecodeError, TypeError) as exc:
            print(f"[Bridge] Invalid JSON: {exc}")
            return

        msg_type = message.get("type")
        handler = self._handlers.get(msg_type)  # type: ignore[arg-type]
        if handler is None:
            print(f"[Bridge] Unknown message type: {msg_type}")
            return

        try:
            handler(message)
        except Exception as exc:
            print(f"[Bridge] Handler error ({msg_type}): {exc}")

    # -- outbound (Python → JS) ---------------------------------------------

    def emit(self, event_name: str, detail: dict) -> None:
        """Dispatch a ``CustomEvent`` to the React frontend.

        Uses ``JSON.parse`` on the JS side so we never need manual escaping.
        """
        json_str = json.dumps(detail)
        # Outer json.dumps wraps the string in quotes for the JS literal
        script = (
            f"window.dispatchEvent(new CustomEvent('{event_name}',"
            f"{{detail:JSON.parse({json.dumps(json_str)})}}));"
        )
        self._webview.CoreWebView2.ExecuteScriptAsync(script)
