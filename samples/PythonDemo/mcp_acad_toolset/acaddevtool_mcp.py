from mcp.server.mcpserver import MCPServer

from prompts.toolset_prompts import register_toolset_prompts
from resources.model_resources import register_model_resources
from resources.static_resources import register_static_resources
from tools.drawing_tools import register_drawing_tools
from tools.query_tools import register_query_tools

mcp = MCPServer("AutoCAD Python Toolset")

register_query_tools(mcp)
register_drawing_tools(mcp)
register_static_resources(mcp)
register_model_resources(mcp)
register_toolset_prompts(mcp)
