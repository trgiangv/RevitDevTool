"""Code execution tools."""
from __future__ import annotations

from typing import Annotated, Any

from mcp.server.fastmcp import FastMCP
from mcp.types import ToolAnnotations
from pydantic import Field

from services.code_execution_service import CodeExecutionService


def register_code_execution_tools(mcp: FastMCP) -> None:
    code_execution_service = CodeExecutionService()

    @mcp.tool(
        annotations=ToolAnnotations(
            title="Execute Revit Code",
            destructiveHint=True,
            openWorldHint=True,
        ),
        structured_output=True,
    )
    async def execute_revit_code(
        code: Annotated[
            str,
            Field(
                description=(
                    "Python code to execute in the Revit context. "
                    "Available in scope: DB (Autodesk.Revit.DB), UI (Autodesk.Revit.UI), "
                    "RevitContext (static class with .ActiveDocument, .ActiveUiDocument, .UiApplication), "
                    "print (captured to output). "
                    "Do NOT create cached global variables — wrap logic in a function and call it immediately."
                ),
            ),
        ],
        description: Annotated[
            str,
            Field(description="Short human-readable label for this execution, used in logs"),
        ] = "Code execution",
    ) -> dict[str, Any]:
        """
        Execute arbitrary Python code inside the Revit process.

        IMPORTANT RULES for generated code:
        - Always wrap your logic in a function and call it immediately to avoid global variable leaks.
        - Access Revit objects through RevitContext — do NOT create temporary aliases like doc = RevitContext.ActiveDocument
          at module level; instead read them inside your function each time.
        - DB and UI modules are pre-imported (from Autodesk.Revit import DB, UI).
        - Use print() to return results — output is captured and returned.
        - For model modifications, wrap changes in a DB.Transaction.
        - Prefer dedicated tools (list_levels, place_family, color_splash, etc.) over this tool when possible.

        Example:
            def run():
                doc = RevitContext.ActiveDocument
                walls = DB.FilteredElementCollector(doc).OfClass(DB.Wall).ToElements()
                print(f"Found {len(list(walls))} walls")
            run()
        """
        return code_execution_service.execute_code(code=code, description=description)
