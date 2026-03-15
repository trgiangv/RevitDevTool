"""Service for direct code execution in Revit context."""

from __future__ import annotations

import io
import traceback as tb

from Autodesk.Revit import DB, UI
from RevitDevTool.Core import RevitContext

from shared.responses import ToolError


class CodeExecutionService:
    def execute_code(self, code: str, description: str = "Code execution") -> dict:
        if not code:
            raise ToolError("No code provided")

        output = io.StringIO()

        def capture_print(*args, **kwargs):
            text = kwargs.get("sep", " ").join(str(arg) for arg in args)
            output.write(text + kwargs.get("end", "\n"))

        scope = {
            "DB": DB,
            "UI": UI,
            "RevitContext": RevitContext,
            "print": capture_print,
        }
        try:
            exec(compile(code, description, "exec"), scope, scope)
            captured = output.getvalue().strip()
            return {"output": captured or "Code executed successfully."}
        except Exception as exc:
            raise ToolError("{}\n{}".format(exc, tb.format_exc())) from exc
