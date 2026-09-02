from mcp.server.mcpserver import MCPServer
from mcp.types import Annotations, Icon
from pydantic import BaseModel

from shared.tool_annotations import read_only_tool

mcp = MCPServer("Parser Annotation Sample")


class ParserStatusOutput(BaseModel):
    status: str


@mcp.tool(
    annotations=read_only_tool(
        "Get Parser Sample Status",
        idempotent=True,
        open_world=False,
    ),
    icons=[
        Icon(
            src="https://example.com/icons/tool.png",
            mime_type="image/png",
            sizes=["16x16"],
        )
    ],
    meta={"feature": "mcpserver", "version": 2},
    structured_output=True,
)
async def get_parser_sample_status() -> ParserStatusOutput:
    return ParserStatusOutput(status="ok")


@mcp.prompt(
    name="summarize_parser_sample",
    title="Summarize Parser Sample",
    description="Build a simple parser validation prompt.",
    icons=[
        Icon(
            src="https://example.com/icons/prompt.png",
            mime_type="image/png",
            sizes=["24x24"],
        )
    ],
)
async def summarize_parser_sample(topic: str, audience: str = "general") -> str:
    return f"Summarize parser status for {topic} and audience {audience}."


@mcp.resource(
    "sample://parser/status",
    name="parser_status_resource",
    title="Parser Status Resource",
    description="Static parser validation resource.",
    mime_type="application/json",
    icons=[
        Icon(
            src="https://example.com/icons/resource-status.png",
            mime_type="image/png",
            sizes=["32x32"],
        )
    ],
    annotations=Annotations(priority=0.9),
    meta={"kind": "status"},
)
def parser_status_resource() -> str:
    return '{"status":"ok"}'


@mcp.resource(
    "sample://parser/views/{view_id}",
    name="parser_view_resource",
    title="Parser View Resource",
    description="Template parser validation resource.",
    mime_type="application/json",
    icons=[
        Icon(
            src="https://example.com/icons/resource-view.png",
            mime_type="image/png",
            sizes=["48x48"],
        )
    ],
    annotations=Annotations(priority=0.6),
    meta={"kind": "view"},
)
def parser_view_resource(view_id: str) -> str:
    return f'{{"viewId":"{view_id}"}}'
