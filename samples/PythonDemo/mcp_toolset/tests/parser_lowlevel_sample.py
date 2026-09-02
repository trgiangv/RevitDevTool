import anyio
from mcp.server.context import ServerRequestContext
from mcp.server.lowlevel import Server
from mcp.types import (
    Annotations,
    GetPromptRequestParams,
    GetPromptResult,
    Icon,
    ListPromptsResult,
    ListResourcesResult,
    ListResourceTemplatesResult,
    ListToolsResult,
    PaginatedRequestParams,
    Prompt,
    PromptArgument,
    PromptMessage,
    Resource,
    ResourceTemplate,
    TextContent,
    Tool,
    ToolAnnotations,
    ToolExecution,
)

PNG_MIME = "image/png"


async def handle_list_tools(
    _ctx: ServerRequestContext,
    _params: PaginatedRequestParams | None,
) -> ListToolsResult:
    await anyio.lowlevel.checkpoint()
    return ListToolsResult(
        tools=[
            Tool(
                name="parser_lowlevel_tool",
                title="Parser Low-Level Tool",
                description="Low-level tool used for parser validation.",
                input_schema={
                    "type": "object",
                    "properties": {
                        "topic": {"type": "string", "description": "Topic to inspect."}
                    },
                    "required": ["topic"],
                },
                output_schema={
                    "type": "object",
                    "properties": {"status": {"type": "string"}},
                    "required": ["status"],
                },
                icons=[
                    Icon(
                        src="https://example.com/icons/lowlevel-tool.png",
                        mime_type=PNG_MIME,
                        sizes=["16x16"],
                    )
                ],
                annotations=ToolAnnotations(
                    title="Parser Low-Level Tool",
                    read_only_hint=True,
                    idempotent_hint=True,
                ),
                meta={"feature": "lowlevel"},
                execution=ToolExecution(task_support="optional"),
            )
        ]
    )


async def handle_list_prompts(
    _ctx: ServerRequestContext,
    _params: PaginatedRequestParams | None,
) -> ListPromptsResult:
    await anyio.lowlevel.checkpoint()
    return ListPromptsResult(
        prompts=[
            Prompt(
                name="parser_lowlevel_prompt",
                title="Parser Low-Level Prompt",
                description="Low-level prompt used for parser validation.",
                icons=[
                    Icon(
                        src="https://example.com/icons/lowlevel-prompt.png",
                        mime_type=PNG_MIME,
                        sizes=["24x24"],
                    )
                ],
                meta={"kind": "prompt"},
                arguments=[
                    PromptArgument(
                        name="topic",
                        description="Topic to focus on.",
                        required=True,
                    )
                ],
            )
        ]
    )


async def handle_get_prompt(
    _ctx: ServerRequestContext,
    params: GetPromptRequestParams,
) -> GetPromptResult:
    await anyio.lowlevel.checkpoint()
    return GetPromptResult(
        description="Low-level prompt used for parser validation.",
        messages=[
            PromptMessage(
                role="user",
                content=TextContent(
                    type="text", text=f"Prompt for {params.arguments or {}}"
                ),
            )
        ],
    )


async def handle_list_resources(
    _ctx: ServerRequestContext,
    _params: PaginatedRequestParams | None,
) -> ListResourcesResult:
    await anyio.lowlevel.checkpoint()
    return ListResourcesResult(
        resources=[
            Resource(
                uri="sample://lowlevel/status",
                name="parser_lowlevel_resource",
                title="Parser Low-Level Resource",
                description="Low-level direct resource used for parser validation.",
                mime_type="text/plain",
                size=128,
                icons=[
                    Icon(
                        src="https://example.com/icons/lowlevel-resource.png",
                        mime_type=PNG_MIME,
                        sizes=["32x32"],
                    )
                ],
                annotations=Annotations(priority=0.8),
                meta={"kind": "resource"},
            )
        ]
    )


async def handle_list_resource_templates(
    _ctx: ServerRequestContext,
    _params: PaginatedRequestParams | None,
) -> ListResourceTemplatesResult:
    await anyio.lowlevel.checkpoint()
    return ListResourceTemplatesResult(
        resource_templates=[
            ResourceTemplate(
                uri_template="sample://lowlevel/items/{item_id}",
                name="parser_lowlevel_template",
                title="Parser Low-Level Template",
                description="Low-level template resource used for parser validation.",
                mime_type="application/json",
                icons=[
                    Icon(
                        src="https://example.com/icons/lowlevel-template.png",
                        mime_type=PNG_MIME,
                        sizes=["48x48"],
                    )
                ],
                annotations=Annotations(priority=0.5),
                meta={"kind": "template"},
            )
        ]
    )


mcp = Server(
    "Parser Low-Level Sample",
    on_list_tools=handle_list_tools,
    on_list_prompts=handle_list_prompts,
    on_get_prompt=handle_get_prompt,
    on_list_resources=handle_list_resources,
    on_list_resource_templates=handle_list_resource_templates,
)
