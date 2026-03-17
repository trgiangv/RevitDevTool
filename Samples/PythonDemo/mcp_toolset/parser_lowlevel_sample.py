import anyio
from mcp import types
from mcp.server.lowlevel import Server


PNG_MIME = "image/png"


async def handle_list_tools(
) -> types.ListToolsResult:
    await anyio.lowlevel.checkpoint()
    return [
        types.Tool(
            name="parser_lowlevel_tool",
            title="Parser Low-Level Tool",
            description="Low-level tool used for parser validation.",
            inputSchema={
                "type": "object",
                "properties": {
                    "topic": {"type": "string", "description": "Topic to inspect."}
                },
                "required": ["topic"],
            },
            outputSchema={
                "type": "object",
                "properties": {
                    "status": {"type": "string"}
                },
                "required": ["status"],
            },
            icons=[types.Icon(src="https://example.com/icons/lowlevel-tool.png", mimeType=PNG_MIME, sizes=["16x16"])],
            annotations=types.ToolAnnotations(title="Parser Low-Level Tool", readOnlyHint=True, idempotentHint=True),
            _meta={"feature": "lowlevel"},
            execution=types.ToolExecution(taskSupport="optional"),
        )
    ]


async def handle_list_prompts(
) -> list[types.Prompt]:
    await anyio.lowlevel.checkpoint()
    return [
        types.Prompt(
            name="parser_lowlevel_prompt",
            title="Parser Low-Level Prompt",
            description="Low-level prompt used for parser validation.",
            icons=[types.Icon(src="https://example.com/icons/lowlevel-prompt.png", mimeType=PNG_MIME, sizes=["24x24"])],
            _meta={"kind": "prompt"},
            arguments=[
                types.PromptArgument(
                    name="topic",
                    description="Topic to focus on.",
                    required=True,
                )
            ],
        )
    ]


async def handle_get_prompt(
    _name: str,
    arguments: dict[str, str] | None,
) -> types.GetPromptResult:
    await anyio.lowlevel.checkpoint()
    return types.GetPromptResult(
        description="Low-level prompt used for parser validation.",
        messages=[
            types.PromptMessage(
                role="user",
                content=types.TextContent(type="text", text=f"Prompt for {arguments or {}}"),
            )
        ],
    )


async def handle_list_resources(
) -> list[types.Resource]:
    await anyio.lowlevel.checkpoint()
    return [
        types.Resource(
            uri="sample://lowlevel/status",
            name="parser_lowlevel_resource",
            title="Parser Low-Level Resource",
            description="Low-level direct resource used for parser validation.",
            mimeType="text/plain",
            size=128,
            icons=[types.Icon(src="https://example.com/icons/lowlevel-resource.png", mimeType=PNG_MIME, sizes=["32x32"])],
            annotations=types.Annotations(priority=0.8),
            _meta={"kind": "resource"},
        )
    ]


async def handle_list_resource_templates(
) -> list[types.ResourceTemplate]:
    await anyio.lowlevel.checkpoint()
    return [
        types.ResourceTemplate(
            uriTemplate="sample://lowlevel/items/{item_id}",
            name="parser_lowlevel_template",
            title="Parser Low-Level Template",
            description="Low-level template resource used for parser validation.",
            mimeType="application/json",
            icons=[types.Icon(src="https://example.com/icons/lowlevel-template.png", mimeType=PNG_MIME, sizes=["48x48"])],
            annotations=types.Annotations(priority=0.5),
            _meta={"kind": "template"},
        )
    ]


mcp = Server("Parser Low-Level Sample")


@mcp.list_tools()
async def list_tools() -> list[types.Tool]:
    return await handle_list_tools()


@mcp.list_prompts()
async def list_prompts() -> list[types.Prompt]:
    return await handle_list_prompts()


@mcp.get_prompt()
async def get_prompt(name: str, arguments: dict[str, str] | None) -> types.GetPromptResult:
    return await handle_get_prompt(name, arguments)


@mcp.list_resources()
async def list_resources() -> list[types.Resource]:
    return await handle_list_resources()


@mcp.list_resource_templates()
async def list_resource_templates() -> list[types.ResourceTemplate]:
    return await handle_list_resource_templates()
