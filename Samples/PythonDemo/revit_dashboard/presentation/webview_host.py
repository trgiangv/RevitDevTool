"""WebView2 window lifecycle: create, initialise, serve, and dispose.

This module owns the WPF ``Window`` + ``WebView2`` control, the virtual-host
mapping, and the ``CoreWebView2`` lifecycle.  Message routing is delegated
entirely to :class:`~revit_dashboard.presentation.bridge.MessageRouter`.

Modes:
- DEV_MODE=True: Navigate to localhost dev server (hot reload)
- DEV_MODE=False: Use built dist files (production)
"""

from __future__ import annotations

import json
import os
import shutil
import tempfile
from pathlib import Path
from typing import TYPE_CHECKING

import Autodesk.Revit.UI as UI
import UIFramework
from collections.abc import Callable
from Microsoft.Web.WebView2.Core import (
    CoreWebView2Environment,
    CoreWebView2HostResourceAccessKind,
)
from Microsoft.Web.WebView2.Wpf import WebView2
from System import Action
from System.Windows import Window, WindowStartupLocation
from System.Windows.Threading import DispatcherPriority

from revit_dashboard.core.event_queue import RevitActionDispatcher
from revit_dashboard.presentation.bridge import MessageRouter

if TYPE_CHECKING:
    from revit_dashboard.contracts.payload import DashboardPayload

# ---------------------------------------------------------------------------
# Configuration
# ---------------------------------------------------------------------------

# Set to True during development to use Vite dev server with hot reload
# Set to False for production to use built dist files
DEV_MODE = False
DEV_SERVER_URL = "http://localhost:5173"

_APP_HOST = "app.local"

# Module-level reference to prevent opening multiple dashboard windows
_active_window: Window | None = None


# ---------------------------------------------------------------------------
# Handler factory type
# ---------------------------------------------------------------------------

HandlerFactory = Callable[[MessageRouter, RevitActionDispatcher], dict[str, Callable[[dict], None]]]


# ---------------------------------------------------------------------------
# Public API
# ---------------------------------------------------------------------------


def show_dashboard(
    payload: DashboardPayload,
    handler_factory: HandlerFactory,
    dev_mode: bool | None = None,
) -> None:
    """Open the React dashboard inside a modeless WPF / WebView2 window.
    
    Args:
        payload: Initial dashboard data from Revit.
        handler_factory: Factory to create message handlers.
        dev_mode: Override DEV_MODE setting. If None, uses module-level DEV_MODE.
    """
    global _active_window  # noqa: PLW0603

    # Guard against double-open: reactivate existing window if still alive
    if _active_window is not None:
        try:
            _active_window.Activate()
            print("[Dashboard] Reactivated existing dashboard window")
            return
        except Exception:
            # Window reference is stale (was closed without proper cleanup)
            _active_window = None

    use_dev_mode = dev_mode if dev_mode is not None else DEV_MODE
    
    try:
        if use_dev_mode:
            print(f"[Dashboard] DEV MODE - Using dev server at {DEV_SERVER_URL}")
            runtime_dist = None
        else:
            runtime_dist = _prepare_runtime(payload)
            print(f"[Dashboard] PRODUCTION MODE - Runtime dist: {runtime_dist}")

        window = _create_window()
        webview = WebView2()
        window.Content = webview

        ctx: dict = {
            "handler_factory": handler_factory,
            "runtime_dist": runtime_dist,
            "payload": payload,
            "dev_mode": use_dev_mode,
            # Will be populated by _on_core_ready:
            "_selection_sub": None,  # tuple[UIApplication, handler] | None
        }

        window.Loaded += lambda _s, _e: _on_window_loaded(window, webview)
        webview.CoreWebView2InitializationCompleted += lambda _s, args: _on_core_ready(
            webview, args, ctx
        )
        window.Closing += lambda _s, _e: _on_window_closing(webview, ctx)
        window.Show()

        _active_window = window
        print("[Dashboard] Modeless WPF window shown")
    except Exception as ex:
        import traceback

        msg = f"Error showing WebView: {ex}\n\n{traceback.format_exc()}"
        print(msg)
        UI.TaskDialog.Show("Error", msg)


# ---------------------------------------------------------------------------
# Window helpers
# ---------------------------------------------------------------------------


def _create_window() -> Window:
    win = Window()
    win.Title = "BIM Dashboard"
    win.Width = 1600
    win.Height = 900
    win.WindowStartupLocation = WindowStartupLocation.CenterOwner
    win.Owner = UIFramework.MainWindow.getMainWnd()
    return win


def _on_window_loaded(window: Window, webview: WebView2) -> None:
    """Start the async WebView2 environment creation.

    ``CoreWebView2Environment.CreateAsync`` is safe outside API context,
    but ``EnsureCoreWebView2Async`` must run *inside* a Revit API context
    (the Revit host validates all calls on the UI thread).  We therefore
    use ``RevitTask.RunAsync`` instead of ``Dispatcher.BeginInvoke``.
    """
    from RevitDevTool.Controllers import ExternalEventController

    print("[Dashboard] Window loaded, initialising WebView2…")
    user_data = _ensure_cache_folder()
    env_task = CoreWebView2Environment.CreateAsync(None, user_data, None)

    def _check():
        if not env_task.IsCompleted:
            return
        try:
            env = env_task.Result
            # Use RevitTask.RunAsync so EnsureCoreWebView2Async runs
            # inside a valid Revit API context on the UI thread.
            ExternalEventController.ActionEventHandler.Raise(
                Action(lambda: webview.EnsureCoreWebView2Async(env)),
            )
            print("[Dashboard] WebView2 environment ready")
        except Exception as ex:
            print(f"[Error] Environment task failed: {ex}")

    env_task.GetAwaiter().OnCompleted(Action(_check))


def _on_core_ready(webview: WebView2, args, ctx: dict) -> None:
    if not args.IsSuccess:
        err = args.InitializationException
        # Log the full exception chain — TargetInvocationException wraps the
        # real cause in InnerException.
        if err is not None:
            inner = getattr(err, "InnerException", None)
            msg = str(err.Message)
            if inner:
                inner_type = inner.GetType().Name if hasattr(inner, "GetType") else type(inner).__name__
                msg += f" -> {inner_type}: {inner.Message}"
            print(f"[Error] WebView2 init failed: {msg}")
        else:
            print("[Error] WebView2 init failed: Unknown (no exception object)")
        return

    handler_factory: HandlerFactory = ctx["handler_factory"]
    use_dev_mode: bool = ctx["dev_mode"]
    payload: DashboardPayload = ctx["payload"]

    # Create the dispatcher using RevitTask.RunAsync for proper API context
    dispatcher = RevitActionDispatcher()

    # Create router with empty handlers, let the factory fill them
    router = MessageRouter(webview, {})
    handlers = handler_factory(router, dispatcher)
    router._handlers = handlers  # noqa: SLF001

    # Set up message handling
    webview.CoreWebView2.WebMessageReceived += lambda _s, ea: router.handle_raw(ea.WebMessageAsJson)
    webview.CoreWebView2.NavigationCompleted += lambda _s, ea: _on_navigation_complete(
        webview, ea, payload, use_dev_mode
    )

    # Subscribe to Revit selection changes for Ghost Mode.
    # _subscribe_selection_changed accesses HOST_APP.uiapp which requires
    # Revit API context — schedule it via RevitTask.RunAsync.
    from RevitDevTool.Controllers import ExternalEventController

    def _setup_selection():
        ctx["_selection_sub"] = _subscribe_selection_changed(router)

    ExternalEventController.ActionEventHandler.Raise(Action(_setup_selection))

    if use_dev_mode:
        # DEV MODE: Navigate to localhost dev server
        print(f"[Dashboard] Navigating to dev server: {DEV_SERVER_URL}")
        webview.CoreWebView2.Navigate(DEV_SERVER_URL)
    else:
        # PRODUCTION MODE: Use virtual host mapping
        runtime_dist: Path = ctx["runtime_dist"]
        webview.CoreWebView2.SetVirtualHostNameToFolderMapping(
            _APP_HOST,
            str(runtime_dist),
            CoreWebView2HostResourceAccessKind.Allow,
        )
        print("[Dashboard] Navigating to virtual host…")
        webview.CoreWebView2.Navigate(f"https://{_APP_HOST}/index.html")


def _subscribe_selection_changed(router: MessageRouter) -> tuple | None:
    """Subscribe to Revit's SelectionChanged event to emit updates to the frontend.

    Returns a ``(uiapp, handler)`` tuple so the caller can unsubscribe later,
    or ``None`` if subscription failed.

    Available since Revit 2019.  If the event is not available (older API),
    we silently skip.
    """
    from revit_dashboard.context import HOST_APP

    try:
        uiapp = HOST_APP.uiapp

        def on_selection_changed(sender, event_args):
            """Emit the current selection to the frontend."""
            try:
                sel_ids = HOST_APP.uidoc.Selection.GetElementIds()
                id_list = [eid.IntegerValue for eid in sel_ids]
                router.emit("revit-selection-changed", {"element_ids": id_list})
            except Exception as exc:
                print(f"[Selection] Error emitting: {exc}")

        uiapp.SelectionChanged += on_selection_changed
        print("[Dashboard] Subscribed to Revit SelectionChanged event")
        return (uiapp, on_selection_changed)
    except AttributeError:
        print("[Dashboard] SelectionChanged event not available on this Revit version")
    except Exception as exc:
        print(f"[Dashboard] Could not subscribe to SelectionChanged: {exc}")
    return None


def _on_navigation_complete(webview: WebView2, ea, payload: DashboardPayload, dev_mode: bool) -> None:
    """Called when navigation completes. Injects payload in dev mode."""
    print(f"[Dashboard] Navigation done. ok={ea.IsSuccess} status={ea.WebErrorStatus}")
    
    if dev_mode and ea.IsSuccess:
        # In dev mode, inject the payload via JavaScript after page loads
        _inject_payload(webview, payload)


def _inject_payload(webview: WebView2, payload: DashboardPayload) -> None:
    """Inject Revit data into the page via JavaScript."""
    try:
        json_str = json.dumps(payload).replace("\\", "\\\\").replace("'", "\\'")
        script = f"window.__BIM_DASHBOARD_INITIAL_DATA = JSON.parse('{json_str}'); window.dispatchEvent(new Event('revit-data-ready'));"
        webview.CoreWebView2.ExecuteScriptAsync(script)
        print("[Dashboard] Payload injected via JavaScript")
    except Exception as ex:
        print(f"[Error] Failed to inject payload: {ex}")


def _on_window_closing(webview: WebView2, ctx: dict) -> None:
    """Clean up all resources when the dashboard window is closing.

    1. Unsubscribe the Revit SelectionChanged event via RevitTask (needs API context)
    2. Dispose the WebView2 control
    3. Clear the module-level active window reference
    """
    global _active_window  # noqa: PLW0603
    print("[Dashboard] Window closing, cleaning up…")

    # 1. Unsubscribe Revit event — requires API context
    sub = ctx.get("_selection_sub")
    if sub is not None:
        from RevitDevTool.Controllers import ExternalEventController

        uiapp_ref, handler_ref = sub

        def _unsub():
            try:
                uiapp_ref.SelectionChanged -= handler_ref
                print("[Dashboard] Unsubscribed from Revit SelectionChanged event")
            except Exception as exc:
                print(f"[Dashboard] Error unsubscribing SelectionChanged: {exc}")

        ExternalEventController.ActionEventHandler.Raise(Action(_unsub))
        ctx["_selection_sub"] = None

    # 2. Dispose WebView2 (WPF operation — no API context needed)
    if webview:
        try:
            webview.Dispose()
        except Exception as exc:
            print(f"[Dashboard] Error disposing WebView2: {exc}")

    # 3. Clear active window reference
    _active_window = None


# ---------------------------------------------------------------------------
# Runtime preparation (production mode only)
# ---------------------------------------------------------------------------


def _prepare_runtime(payload: DashboardPayload) -> Path:
    """Copy the built React app to a temp folder and inject the initial payload."""
    source_dist = Path(__file__).resolve().parents[2] / "revit_dashboard_ui" / "dist"
    runtime_root = Path(tempfile.mkdtemp(prefix="revit_bim_dashboard_"))
    runtime_dist = runtime_root / "dist"

    if source_dist.exists():
        shutil.copytree(source_dist, runtime_dist)
    else:
        runtime_dist.mkdir(parents=True, exist_ok=True)
        (runtime_dist / "index.html").write_text(
            "<html><body><h3>dashboard-ui/dist not found.</h3>"
            "<p>Run: cd revit_dashboard_ui && npm run build</p></body></html>",
            encoding="utf-8",
        )

    index = runtime_dist / "index.html"
    data_script = (
        "<script>window.__BIM_DASHBOARD_INITIAL_DATA="
        + json.dumps(payload).replace("</", "<\\/")
        + ";</script>"
    )
    html = index.read_text(encoding="utf-8")
    html = html.replace("</head>", data_script + "\n</head>") if "</head>" in html else data_script + html
    index.write_text(html, encoding="utf-8")
    return runtime_dist


def _ensure_cache_folder() -> str:
    """Return a usable WebView2 user-data folder.

    If the fixed-path folder is locked by a previously crashed instance,
    nuke it and recreate to avoid ``TargetInvocationException`` on init.
    """
    folder = os.path.join(tempfile.gettempdir(), "Revit_Dashboard_WV2_Cache")
    try:
        os.makedirs(folder, exist_ok=True)
        # Probe: verify the folder is writable (not locked)
        probe = os.path.join(folder, ".lock_test")
        with open(probe, "w") as f:
            f.write("ok")
        os.remove(probe)
    except OSError:
        print("[Dashboard] Cache folder locked — clearing stale cache")
        shutil.rmtree(folder, ignore_errors=True)
        os.makedirs(folder, exist_ok=True)
    return folder
