"""Launch and discovery tools for Revit instances."""
from __future__ import annotations

import anyio
from typing import Annotated, Any

from mcp.server.fastmcp import FastMCP
from mcp.types import ToolAnnotations
from pydantic import Field

from services.launch_service import LaunchService
from shared.responses import ToolError


def register_launch_tools(mcp: FastMCP) -> None:
    launch_service = LaunchService()

    @mcp.tool(
        annotations=ToolAnnotations(
            title="List Revit Installations",
            readOnlyHint=True,
            idempotentHint=True,
            openWorldHint=True,
        ),
        structured_output=True,
    )
    async def list_revit_installations() -> dict[str, Any]:
        """
        List installed Revit versions discovered on this machine.

        Workflow:
        - Call before launch_revit to see available versions.
        - Use the version year (e.g. '2025') when launching.
        - Helps determine which version to use for compatibility with the target file.
        """
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
        file_path: Annotated[
            str | None,
            Field(description="Absolute path to a .rvt file to open on launch; launches empty session if omitted"),
        ] = None,
        version: Annotated[
            str | None,
            Field(description="Revit version year to launch, e.g. '2025'; uses latest installed if omitted"),
        ] = None,
        language: Annotated[
            str | None,
            Field(description="UI language code, e.g. 'ENU' for English; uses system default if omitted"),
        ] = None,
        launch_timeout_seconds: Annotated[
            int,
            Field(description="Seconds to wait for Revit to become ready before timing out", ge=10, le=600),
        ] = 120,
    ) -> dict[str, Any]:
        """
        Launch Revit, optionally opening a file.

        Workflow:
        - Call to start Revit. For empty session, omit file_path and use open_document after launch.
        - For workshared files, Revit may show its native dialog; use open_document for programmatic control.
        - Use get_revit_status after launch to verify the session is ready.
        """
        try:
            with anyio.fail_after(float(launch_timeout_seconds)):
                return await launch_service.launch_revit(
                    file_path=file_path,
                    version=version,
                    language=language,
                    timeout_seconds=int(launch_timeout_seconds),
                )
        except TimeoutError:
            raise ToolError(
                "Launch operation timed out after {} seconds.".format(int(launch_timeout_seconds)),
                code="revit.launch_timeout",
            ) from None
