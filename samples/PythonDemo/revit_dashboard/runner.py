"""Dashboard entry point — orchestrates collect → analytics → webview.

Wires message handlers, the Revit action dispatcher, and the export callback
into the WebView2 host via a handler factory.

IMPORTANT: Always use HOST_APP.doc to access document, never cache it.
"""

from collections.abc import Callable
from typing import TYPE_CHECKING

import Autodesk.Revit.UI as UI

from revit_dashboard.analytics.engine import build_dashboard_payload
from revit_dashboard.context import HOST_APP
from revit_dashboard.data.collector import collect_model_elements, collect_model_info
from revit_dashboard.data.heavy_families import collect_heavy_families
from revit_dashboard.data.warnings import collect_warnings
from revit_dashboard.export.excel_exporter import export_filtered_rows_to_excel
from revit_dashboard.presentation.webview_host import show_dashboard
from revit_dashboard.revit_api.handler import RevitApiHandler

if TYPE_CHECKING:
    from revit_dashboard.core.event_queue import RevitActionDispatcher
    from revit_dashboard.presentation.bridge import MessageRouter


def main() -> None:
    """Execute the dashboard workflow."""
    try:
        print("Starting Revit Elements Analysis…")

        # Always use HOST_APP.doc to get the current document
        print("Collecting model elements…")
        all_rows_ref: list[list[dict]] = [collect_model_elements(HOST_APP.doc)]
        if not all_rows_ref[0]:
            UI.TaskDialog.Show("Warning", "No elements found in the current Revit document.")
            return
        print(f"Collected {len(all_rows_ref[0])} model elements")

        # Collect supplementary data
        print("Collecting model info…")
        model_info = collect_model_info(HOST_APP.doc)
        print("Collecting warnings…")
        warnings = collect_warnings(HOST_APP.doc)
        print(f"Found {len(warnings)} warnings")
        print("Collecting heavy families…")
        heavy_families = collect_heavy_families(all_rows_ref[0])

        print("Building analytics payload…")
        payload = build_dashboard_payload(
            all_rows_ref[0],
            filters=None,
            model_info=model_info,
            warnings=warnings,
            heavy_families=heavy_families,
        )

        # Revit API handler - uses HOST_APP internally
        def refresh_data():
            print("[Refresh] Re-collecting from Revit…")
            # Always use HOST_APP.doc for fresh document reference
            all_rows_ref[0] = collect_model_elements(HOST_APP.doc)
            print(f"[Refresh] {len(all_rows_ref[0])} elements")
            fresh_model_info = collect_model_info(HOST_APP.doc)
            fresh_warnings = collect_warnings(HOST_APP.doc)
            fresh_heavy = collect_heavy_families(all_rows_ref[0])
            return build_dashboard_payload(
                all_rows_ref[0],
                filters=None,
                model_info=fresh_model_info,
                warnings=fresh_warnings,
                heavy_families=fresh_heavy,
            )

        revit_handler = RevitApiHandler(refresh_callback=refresh_data)

        # Handler factory — receives MessageRouter + RevitActionDispatcher once WebView2 is ready
        def handler_factory(
            router: MessageRouter,
            dispatcher: RevitActionDispatcher,
        ) -> dict[str, Callable[[dict], None]]:
            return _build_handlers(router, dispatcher, all_rows_ref, revit_handler)

        print("Launching BIM dashboard…")
        show_dashboard(payload, handler_factory)

    except Exception as ex:
        import traceback

        msg = f"Error: {ex}\n\n{traceback.format_exc()}"
        print(msg)
        UI.TaskDialog.Show("Error", msg)


def _build_handlers(
    router: MessageRouter,
    dispatcher: RevitActionDispatcher,
    all_rows_ref: list[list[dict]],
    revit_handler: RevitApiHandler,
) -> dict[str, Callable[[dict], None]]:
    """Create message handler dict wired to the given ``router``."""

    def handle_export(message: dict) -> None:
        payload_obj = message.get("payload") or {}
        filters = payload_obj.get("filters") or {}
        try:
            print("[Export] Opening Save File dialog…")
            path = export_filtered_rows_to_excel(all_rows_ref[0], filters=filters)
            if path is None:
                print("[Export] Cancelled by user")
                router.emit("bim-export-result", {"ok": False, "error": "Export cancelled"})
                return
            print(f"[Export] Done: {path}")
            router.emit("bim-export-result", {"ok": True, "path": path})
        except Exception as exc:
            print(f"[Export] Error: {exc}")
            router.emit("bim-export-result", {"ok": False, "error": str(exc)})

    def handle_log(message: dict) -> None:
        payload_obj = message.get("payload") or {}
        print("[UI] " + str(payload_obj.get("message", "")))

    def handle_revit_api(message: dict) -> None:
        method = message.get("method", "")
        params = message.get("params", {})
        msg_id = message.get("id")

        def action():
            return revit_handler.handle(method, params)

        def on_complete(result):
            if isinstance(result, dict):
                result["id"] = msg_id
            else:
                result = {"id": msg_id, "ok": True, "data": result}
            router.emit("revit-api-result", result)

        dispatcher.enqueue(action, on_complete)

    return {
        "export_excel": handle_export,
        "log": handle_log,
        "revit_api": handle_revit_api,
    }
