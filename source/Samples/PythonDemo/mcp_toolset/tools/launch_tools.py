"""Launch and discovery tools for Revit instances."""

import anyio
from typing import Any

from mcp.server.fastmcp import Context, FastMCP
from mcp.types import ToolAnnotations

from services.launch_service import LaunchService
from shared.responses import ToolError
from utils import try_log


def register_launch_tools(mcp: FastMCP, launch_service: LaunchService) -> None:
    @mcp.tool(
        annotations=ToolAnnotations(
            title="List Revit Installations",
            readOnlyHint=True,
            idempotentHint=True,
            openWorldHint=True,
        ),
        structured_output=True,
    )
    async def list_revit_installations(ctx: Context | None = None) -> dict[str, Any]:
        """List installed Revit versions discovered on this machine."""
        _ = ctx
        return launch_service.list_revit_installations()

    @mcp.tool(
        annotations=ToolAnnotations(
            title="Launch Revit",
            destructiveHint=True,
            openWorldHint=True,
        ),
        structured_output=True,
    )
    async def launch_revit(
        ctx: Context,
        file_path: str | None = None,
        version: str | None = None,
        language: str | None = None,
        launch_timeout_seconds: int = 120,
    ) -> dict[str, Any]:
        """Launch Revit, optionally opening a file."""
        timeout_seconds = launch_timeout_seconds
        try:
            timeout_seconds = float(timeout_seconds)
        except (TypeError, ValueError):
            timeout_seconds = 120.0

        await try_log(ctx, "info", "Launching Revit...")
        try:
            with anyio.fail_after(timeout_seconds):
                return await launch_service.launch_revit(
                    file_path=file_path,
                    version=version,
                    language=language,
                    timeout=int(timeout_seconds),
                )
        except TimeoutError:
            raise ToolError(
                "Launch operation timed out after {} seconds.".format(int(timeout_seconds)),
                code="revit.launch_timeout",
            ) from None
