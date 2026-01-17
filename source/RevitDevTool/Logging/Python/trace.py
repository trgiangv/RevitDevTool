# -*- coding: utf-8 -*-
"""
RevitDevTool Python Helper
Utility functions for enhanced logging with Python stack trace support.

Compatible with: IronPython 2.7, IronPython 3.4, CPython 3.x

Usage:
    # depend on how your module is structured, import like this:
    from trace import trace # trace.py same folder with your script
    from revit_dev_tool.trace import trace # if revit_dev_tool is a package

    def my_function():
        trace("Processing element")
        trace("Info message")
        trace("Warning!")
        trace("Error!")
"""

import clr
import traceback
from pyrevit.compat import NETCORE
from pyrevit import EXEC_PARAMS

HAS_RDT = False
try:
    clr.AddReference("RevitDevTool")
    from RevitDevTool.Logging.Python import PyTrace # type: ignore
    HAS_RDT = True
except Exception:
    if NETCORE:
        clr.AddReference("System.Diagnostics.TraceSource")
    from System.Diagnostics import Trace

def trace(message):
    """
    Write a trace message with Python traceback.

    Arguments:
        message (object): The value to log.
    """
    stack = traceback.extract_stack()

    # ignore the last frame which is this function call
    clean_stack = stack[:-1]
    clean_stack.reverse()

    formatted_lines = []
    for frame in clean_stack:
        file_path = getattr(frame, 'filename', frame[0])
        func_name = getattr(frame, 'name', frame[2])
        if file_path == "<string>":
            file_path = EXEC_PARAMS.command_path
        formatted_lines.append('  File "{0}", in {1}'.format(file_path, func_name))

    stack_str = "\n".join(formatted_lines)

    if HAS_RDT:
        PyTrace.Write(message, stack_str)
    else:
        Trace.Write(message)

