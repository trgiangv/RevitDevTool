# -*- coding: utf-8 -*-
"""Dashboard window.

The caller builds the WPF window and injects its WebView2 control.
Environment setup and start navigation stay in ``WebViewControl``.
"""

from __future__ import division, print_function

import os
import threading
import traceback

import UIFramework
from Autodesk.Revit import DB, UI
from Autodesk.Revit.UI.Events import SelectionChangedEventArgs
from System import EventArgs
from System.Windows import Window, WindowStartupLocation

from pyrevit_webview import (
    CommandRequest,
    EnvelopeField,
    event_message,
    parse_request,
    response_message,
)
from pyrevit_webview.adapter.revit import REVIT_CONTEXT
from pyrevit_webview.bridge.envelope import Envelope
from pyrevit_webview.compat import Callable
from pyrevit_webview.host import (
    PackagedContent,
    RemoteContent,
    WebViewControl,
    WebViewSession,
    WebViewSettings,
)
from pyrevit_webview.host.wpf import WebView2, WpfUi
from pyrevit_webview.serialize import serialize

from revit_dashboard.context import HOST_APP
from revit_dashboard.log import get_logger
from revit_dashboard.revit_api.handler import RevitApiHandler

# True navigates to the Vite server. False maps app.local onto the built dist.
DEV_MODE = True
DEV_SERVER_URL = "http://localhost:5173"
DEV_CDP_PORT = 9225

_APP_HOST = "app.local"
_ROW_CHUNK = 250
_LATEST_COMMANDS = ["ColorOverride", "Isolate", "ApplyGroupOverrides"]

_active_window = None  # type: Window | None
log = get_logger()


class _CommandQueue(object):
    """One Revit consumer. The WebView callback only enqueues.

    ``_LATEST_COMMANDS`` keep the newest pending request of that name.
    A dropped request is answered immediately so its promise settles.
    """

    def __init__(self, gate):
        # type: (_DashboardGate) -> None
        self._gate = gate  # type: _DashboardGate
        self._pending = []  # type: list[CommandRequest]
        self._running = False  # type: bool
        self._lock = threading.Lock()

    def add(self, parsed):
        # type: (CommandRequest) -> None
        dropped = []
        start = False
        with self._lock:
            command = parsed.command
            if command in _LATEST_COMMANDS:
                dropped = self._take_pending(command)
            self._pending.append(parsed)
            if not self._running:
                self._running = True
                start = True
        for item in dropped:
            self._gate.reply_dropped(item)
        if start:
            REVIT_CONTEXT.post(self._pump)

    def _take_pending(self, command):
        # type: (str) -> list[CommandRequest]
        dropped = []
        kept = []
        for item in self._pending:
            if item.command == command:
                dropped.append(item)
            else:
                kept.append(item)
        self._pending = kept
        return dropped

    def _pump(self):
        # type: () -> None
        with self._lock:
            if not self._pending:
                self._running = False
                return
            parsed = self._pending.pop(0)
        try:
            self._gate._run_request(parsed)
        finally:
            REVIT_CONTEXT.post(self._pump)


class _DashboardGate(object):
    """WebView callbacks that need the Revit API go through ``REVIT_CONTEXT.post``."""

    def __init__(self, api):
        # type: (RevitApiHandler) -> None
        self._api = api  # type: RevitApiHandler
        self._control = None  # type: WebViewControl | None
        self._selection_sub = None  # type: tuple[UI.UIApplication, Callable[[UI.UIApplication, SelectionChangedEventArgs], None]] | None
        self._queue = _CommandQueue(self)  # type: _CommandQueue
        self._window = None  # type: Window | None

    def attach(self, control, window):
        # type: (WebViewControl, Window) -> None
        self._control = control
        self._window = window

    def handle_message(self, raw_json):
        # type: (str) -> None
        parsed = parse_request(raw_json)
        if parsed is None:
            return
        self._queue.add(parsed)

    def reply_dropped(self, parsed):
        # type: (CommandRequest) -> None
        self._post(response_message(
            parsed.command,
            parsed.id,
            payload={EnvelopeField.OK: True},
        ))

    def subscribe(self):
        # type: () -> None
        REVIT_CONTEXT.post(self._subscribe)

    def close(self):
        # type: () -> None
        REVIT_CONTEXT.post(self._unsubscribe)

    def _run_request(self, parsed):
        # type: (CommandRequest) -> None
        command = parsed.command
        msg_id = parsed.id
        if command == "SaveWorkbook":
            self._save_workbook(parsed)
            return
        if not self._api.knows(command):
            self._post(response_message(
                command,
                msg_id,
                error="Unknown command: {0}".format(command),
            ))
            return
        try:
            result = self._api.run(command, parsed.payload)
            self._post(response_message(command, msg_id, payload=result))
        except Exception as exc:
            log.error("command {0} failed: {1}".format(command, exc))
            self._post(response_message(command, msg_id, error=str(exc)))

    def _save_workbook(self, parsed):
        # type: (CommandRequest) -> None
        try:
            result = _write_workbook(self._window, parsed.payload)
            self._post(response_message(parsed.command, parsed.id, payload=result))
        except Exception as exc:
            log.error("command SaveWorkbook failed: {0}".format(exc))
            self._post(response_message(parsed.command, parsed.id, error=str(exc)))

    def _subscribe(self):
        # type: () -> None
        if self._selection_sub is not None:
            return
        try:
            uiapp = HOST_APP.uiapp

            def on_selection_changed(sender, event_args):
                # type: (UI.UIApplication, SelectionChangedEventArgs) -> None
                try:
                    selected = HOST_APP.uidoc.Selection.GetElementIds()
                    element_ids = []  # type: list[str]
                    for eid in selected:
                        if eid.Equals(DB.ElementId.InvalidElementId):
                            continue
                        element_ids.append(eid.ToString())
                    self._post(event_message(
                        "selection-changed",
                        {"element_ids": element_ids},
                    ))
                except Exception as exc:
                    log.error("selection event failed: {0}".format(exc))

            uiapp.SelectionChanged += on_selection_changed
            self._selection_sub = (uiapp, on_selection_changed)
            log.info("subscribed to SelectionChanged")
        except AttributeError:
            log.warning("SelectionChanged is not available on this Revit version")
        except Exception as exc:
            log.error("could not subscribe to SelectionChanged: {0}".format(exc))

    def _unsubscribe(self):
        # type: () -> None
        sub = self._selection_sub
        self._selection_sub = None
        if sub is None:
            return
        uiapp_ref, handler_ref = sub
        try:
            uiapp_ref.SelectionChanged -= handler_ref
            log.info("unsubscribed from SelectionChanged")
        except Exception as exc:
            log.error("unsubscribe SelectionChanged failed: {0}".format(exc))

    def _post(self, message):
        # type: (Envelope) -> None
        payload = message.get(EnvelopeField.PAYLOAD) if isinstance(message, dict) else None
        rows = payload.get("rows") if isinstance(payload, dict) else None
        if not isinstance(rows, list) or len(rows) <= _ROW_CHUNK:
            self._emit(message)
            return

        msg_id = message.get(EnvelopeField.ID)
        total = len(rows)
        start = 0
        while start < total:
            end = start + _ROW_CHUNK
            self._emit(event_message("snapshot-rows", {
                EnvelopeField.ID: msg_id,
                "start": start,
                "rows": rows[start:end],
            }))
            start = end

        slim = dict(payload)
        del slim["rows"]
        slim["rows_chunked"] = True
        slim["row_count"] = total
        reply = dict(message)
        reply[EnvelopeField.PAYLOAD] = slim
        self._emit(reply)
        log.info("posted {0} rows in chunks of {1}".format(total, _ROW_CHUNK))

    def _emit(self, message):
        # type: (Envelope) -> None
        """Deliver one envelope with ``ExecuteScriptAsync``.

        ``PostWebMessageAsJson`` queues on the dispatcher and this page never
        observed those messages. The library already reaches the page by
        dispatching a ``CustomEvent`` from script.
        """
        encoded = serialize(message)
        literal = serialize(encoded)
        script = (
            "window.dispatchEvent(new CustomEvent('bim-bridge',{detail:JSON.parse("
            + literal
            + ")}));"
        )
        if self._control is None or not self._control.is_available:
            log.warning("WebView is not available")
            return
        try:
            self._control.execute_script(script)
        except Exception as exc:
            log.error("emit failed ({0} chars): {1}".format(len(script), exc))


def show_dashboard():
    # type: () -> None
    """Open one modeless dashboard window, or activate the existing one."""
    global _active_window

    if _active_window is not None:
        try:
            _active_window.Activate()
            log.info("reactivated existing dashboard window")
            return
        except Exception:
            _active_window = None

    REVIT_CONTEXT.initialize(HOST_APP.uiapp)

    try:
        gate = _DashboardGate(RevitApiHandler())
        window = _create_window()
        webview = WebView2()
        window.Content = webview
        control = WebViewControl(
            webview,
            _session(),
            [],
            WpfUi(window, webview),
            on_browser_ready=lambda _ready: gate.subscribe(),
            on_web_message=gate.handle_message,
        )
        gate.attach(control, window)

        def on_closed(sender, args):
            # type: (Window, EventArgs) -> None
            global _active_window
            log.info("window closing")
            gate.close()
            control.dispose()
            _active_window = None

        window.Closed += on_closed
        window.Show()
        _active_window = window
        log.info("modeless window shown")
    except Exception as ex:
        log.exception("showing WebView failed")
        UI.TaskDialog.Show(
            "Error",
            "Error showing WebView: {0}\n\n{1}".format(ex, traceback.format_exc()),
        )


def _write_workbook(window, payload):
    # type: (Window | None, object) -> dict
    """Show the dashboard window Save dialog and write the workbook bytes."""
    from Microsoft.Win32 import SaveFileDialog
    from System import Convert
    from System.IO import File

    if window is None:
        raise RuntimeError("dashboard window is closed")
    if not isinstance(payload, dict):
        raise ValueError("workbook payload is missing")
    content = payload.get("content_base64")
    if not content:
        raise ValueError("workbook is empty")

    filename = os.path.basename(str(payload.get("filename") or "bim_dashboard_export.xlsx"))
    if not filename.lower().endswith(".xlsx"):
        filename = "{0}.xlsx".format(filename)

    dialog = SaveFileDialog()
    dialog.Title = "Export Dashboard"
    dialog.Filter = "Excel workbook (*.xlsx)|*.xlsx"
    dialog.FileName = filename
    dialog.DefaultExt = ".xlsx"
    dialog.AddExtension = True
    dialog.OverwritePrompt = True
    accepted = dialog.ShowDialog(window)
    if accepted is not True and getattr(accepted, "Value", None) is not True:
        return {"cancelled": True}

    File.WriteAllBytes(dialog.FileName, Convert.FromBase64String(content))
    return {"cancelled": False, "path": dialog.FileName}


def _create_window():
    # type: () -> Window
    window = Window()
    window.Title = "BIM Dashboard"
    window.Width = 1600
    window.Height = 900
    window.WindowStartupLocation = WindowStartupLocation.CenterOwner
    owner = UIFramework.MainWindow.getMainWnd()
    if owner is not None:
        window.Owner = owner
    return window


def _session():
    # type: () -> WebViewSession
    if DEV_MODE:
        content = RemoteContent(DEV_SERVER_URL)
    else:
        content = PackagedContent(
            "https://{0}/index.html".format(_APP_HOST),
            _dist_folder(),
        )
    return WebViewSession(
        content,
        app_name="RevitDashboard",
        debug_cdp_port=DEV_CDP_PORT if DEV_MODE else None,
        settings=WebViewSettings(dev_tools_enabled=DEV_MODE),
    )


def _dist_folder():
    # type: () -> str
    here = os.path.dirname(os.path.abspath(__file__))
    python_demo = os.path.dirname(os.path.dirname(here))
    return os.path.join(python_demo, "revit_dashboard_ui", "dist")
