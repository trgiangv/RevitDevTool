"""Code execution tools."""

from typing import Annotated, Any

from mcp.server.fastmcp import Context, FastMCP
from mcp.types import ToolAnnotations
from pydantic import Field

from services.code_execution_service import CodeExecutionService
from utils import try_log


def register_code_execution_tools(mcp: FastMCP, code_execution_service: CodeExecutionService) -> None:
    @mcp.tool(
        annotations=ToolAnnotations(
            title="Execute Revit Code",
            destructiveHint=True,
            openWorldHint=True,
        ),
        structured_output=True,
    )
    async def execute_revit_code(
        code: Annotated[str, Field(description="Python code to execute in the Revit context. Has access to 'doc', 'app', 'uidoc' globals.")],
        description: Annotated[str, Field(description="Short human-readable label for this execution, used in logs")] = "Code execution",
        ctx: Context | None = None,
    ) -> dict[str, Any]:
        """Execute arbitrary Python code in the Revit context."""
        await try_log(ctx, "info", "Executing code: {}".format(description))
        return code_execution_service.execute_code(code=code, description=description)
