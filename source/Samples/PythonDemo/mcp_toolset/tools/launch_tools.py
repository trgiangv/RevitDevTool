"""Launch and discovery tools for Revit instances."""

import anyio
from typing import Annotated, Any

from mcp.server.fastmcp import Context, FastMCP
from mcp.types import ToolAnnotations
from pydantic import Field

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
        file_path: Annotated[str | None, Field(description="Absolute path to a .rvt file to open on launch; launches empty session if omitted")] = None,
        version: Annotated[str | None, Field(description="Revit version year to launch, e.g. '2025'; uses latest installed if omitted")] = None,
        language: Annotated[str | None, Field(description="UI language code, e.g. 'ENU' for English; uses system default if omitted")] = None,
        launch_timeout_seconds: Annotated[int, Field(description="Seconds to wait for Revit to become ready before timing out", ge=10, le=600)] = 120,
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
