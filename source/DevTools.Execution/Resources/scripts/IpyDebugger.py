# coding: utf-8  # noqa: UP009
"""IpyDebugger: IronPython 2.7 / 3.4 pydevd listen + debugpy DAP handshake.

Host injects ``__port__`` then execs this file. Do not import from user scripts.

IronPython 3.4 reports ``sys.platform`` as win32. pydevd 2.8.0 sets
``IS_IRONPYTHON`` from platform == 'cli' and ``IS_WINDOWS`` from
platform == 'win32'. Those two cannot be true at once, so: import
constants under cli, force ``IS_WINDOWS``, restore platform, then import
pydevd. Leaving ``IS_WINDOWS`` false makes breakpoint path matching
case-sensitive (Samples vs samples, C: vs c:) and never hits.

Before ``_enable_attach``, wrap ``os.path.abspath`` so it does not throw.
``import site`` (from pydevd FilesFiltering) calls ``abspath`` on CLR
``__file__`` assembly names; net48 ``Path.GetFullPath`` rejects them.
Do not edit AppData pydevd.

VS Code/Cursor ``type: debugpy`` attach+connect talks DAP directly to this
socket. pydevd 2.8 never emits InitializedEvent after attach. Without the
event, the client waits forever and never setBreakpoints. Emit it when
adapterID is debugpy.

Unhandled DAP commands in 2.8 print and return with no response. Reply
success=False instead.

Expanding CLR/Revit objects getattr-throws (FamilyCreate, worksets).
pydevd 2.8 dumps that traceback to stderr; the host maps ``exception`` to
ERR. Store a one-line value instead. Do not edit AppData pydevd.
"""

import json
import os
import sys


class IpyDebugger:
    def start_listening(self, port):
        orig_platform = sys.platform
        sys.platform = "cli"
        from _pydevd_bundle import pydevd_constants

        pydevd_constants.IS_WINDOWS = True
        sys.platform = orig_platform

        from _pydevd_bundle.pydevd_constants import HTTP_JSON_PROTOCOL
        from _pydevd_bundle.pydevd_defaults import PydevdCustomization

        PydevdCustomization.DEFAULT_PROTOCOL = HTTP_JSON_PROTOCOL

        import pydevd

        orig_abspath = os.path.abspath

        def _abspath(path):
            try:
                return orig_abspath(path)
            except Exception:
                return path or os.curdir

        os.path.abspath = _abspath
        os.path.realpath = _abspath
        pydevd._enable_attach(("127.0.0.1", int(port)))
        # 2.8 HTTP_JSON make_thread_suspend_message is NULL_NET_COMMAND.
        # DAP StoppedEvent is only sent when this flag is True. 3.4.1
        # (VS Code adapter) still emits StoppedEvent when the flag is False;
        # 2.8 does not. Adapter never sends multiThreadsSingleNotification.
        pydevd.get_global_debugger().multi_threads_single_notification = True
        self._install_debugpy_dap_shim()
        self._install_quiet_variable_resolver()

    def _install_debugpy_dap_shim(self):
        from _pydevd_bundle._debug_adapter.pydevd_schema import InitializedEvent
        from _pydevd_bundle.pydevd_comm_constants import CMD_RETURN
        from _pydevd_bundle.pydevd_net_command import NetCommand
        from _pydevd_bundle.pydevd_process_net_command_json import (
            PyDevJsonCommandProcessor,
        )

        orig_initialize = PyDevJsonCommandProcessor.on_initialize_request
        orig_handle = PyDevJsonCommandProcessor._handle_launch_or_attach_request
        orig_process = PyDevJsonCommandProcessor.process_net_command_json

        def on_initialize_request(processor, py_db, request):
            try:
                adapter_id = request.arguments.adapterID or ""
            except:  # noqa
                adapter_id = ""
            processor._debugpy_direct_client = adapter_id == "debugpy"
            return orig_initialize(processor, py_db, request)

        def handle_launch_or_attach_request(processor, py_db, request, start_reason):
            cmd = orig_handle(processor, py_db, request, start_reason)
            if getattr(processor, "_debugpy_direct_client", False):
                py_db.writer.add_command(
                    NetCommand(CMD_RETURN, 0, InitializedEvent(), is_json=True)
                )
            return cmd

        def process_net_command_json(processor, py_db, json_contents, send_response=True):
            raw = (
                json_contents.decode("utf-8")
                if isinstance(json_contents, bytes)
                else json_contents
            )
            loaded = json.loads(raw)
            command = loaded.get("command", "")
            method_name = "on_{}_request".format(command.lower())  # noqa: UP032
            if command and getattr(processor, method_name, None) is None:
                py_db.writer.add_command(
                    NetCommand(
                        CMD_RETURN,
                        0,
                        {
                            "type": "response",
                            "request_seq": loaded.get("seq", 0),
                            "success": False,
                            "command": command,
                            "message": "Unhandled DAP command in pydevd 2.8: {}".format(command),  # noqa: UP032
                        },
                        is_json=True,
                    )
                )
                return None

            return orig_process(processor, py_db, json_contents, send_response)

        PyDevJsonCommandProcessor.on_initialize_request = on_initialize_request
        PyDevJsonCommandProcessor._handle_launch_or_attach_request = (
            handle_launch_or_attach_request
        )
        PyDevJsonCommandProcessor.process_net_command_json = process_net_command_json

    def _install_quiet_variable_resolver(self):
        from _pydevd_bundle import pydevd_resolver

        def _short_print_exc(file=None):
            if file is None:
                return
            try:
                exc = sys.exc_info()[1]
                if exc is not None:
                    file.write("<{}>".format(exc))  # noqa: UP032
            except:  # noqa
                pass

        pydevd_resolver.traceback.print_exc = _short_print_exc

        orig_get = pydevd_resolver.DefaultResolver._get_py_dictionary
        orig_resolve = pydevd_resolver.DefaultResolver.resolve

        def _get_py_dictionary(resolver, var, names=None, used___dict__=False):
            saved_err = sys.stderr
            sys.stderr = _NullWriter()
            try:
                return orig_get(resolver, var, names, used___dict__)
            finally:
                sys.stderr = saved_err

        def resolve(resolver, var, attribute):
            saved_err = sys.stderr
            sys.stderr = _NullWriter()
            try:
                return orig_resolve(resolver, var, attribute)
            except Exception as e:  # noqa: BLE001
                return "<{}>".format(e)  # noqa: UP032
            finally:
                sys.stderr = saved_err

        pydevd_resolver.DefaultResolver._get_py_dictionary = _get_py_dictionary
        pydevd_resolver.DefaultResolver.resolve = resolve


class _NullWriter:
    def write(self, *args, **kwargs):
        # this is a null writer that ignores all writes, used to suppress stderr output
        pass

    def flush(self):
        # this is a null flush that does nothing, used to suppress stderr output
        pass


IpyDebugger().start_listening(__port__)  # noqa: F821  # pyright: ignore[reportUndefinedVariable]
