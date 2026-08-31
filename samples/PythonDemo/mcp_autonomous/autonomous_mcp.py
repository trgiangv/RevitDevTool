from typing import Annotated

from Autodesk.Revit import UI
from mcp.server.mcpserver import MCPServer
from mcp.types import CallToolResult, TextContent, ToolAnnotations
from pydantic import Field
from RevitDevTool.Core import RevitContext

mcp = MCPServer("Autonomous Revit Toolset")


def register_autonomous_tools(mcp: MCPServer) -> None:

    @mcp.tool(annotations=ToolAnnotations(title="Probe / Post Revit Command"))
    async def autonomous_post_command(
        command_id: Annotated[str, Field(description="Revit command id for LookupCommandId")],
        post: Annotated[
            bool,
            Field(description="If true, call PostCommand when CanPostCommand is true"),
        ] = False,
    ) -> CallToolResult:
        """Temporary spike: probe LookupCommandId / CanPostCommand / PostCommand."""

        uiapp = RevitContext.UiApplication
        cmd = UI.RevitCommandId.LookupCommandId(command_id)
        found = cmd is not None
        can_post = uiapp.CanPostCommand(cmd) if found else False

        lines = [
            f"command_id={command_id}",
            f"found={found}",
            f"can_post={can_post}",
        ]
        if found:
            lines.append(f"name={cmd.Name}")
            lines.append(f"id={cmd.Id}")

        if post and can_post:
            uiapp.PostCommand(cmd)
            lines.append("posted=true")
        elif post:
            lines.append("posted=false (CanPostCommand was false)")

        return CallToolResult(content=[TextContent(type="text", text="\n".join(lines))])


register_autonomous_tools(mcp)
