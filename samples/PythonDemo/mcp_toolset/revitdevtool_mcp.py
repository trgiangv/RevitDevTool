# /// script
# dependencies = [
#     "polars",
#     "xlsxwriter",
# ]
# ///
from mcp.server.fastmcp import FastMCP

mcp = FastMCP("Revit Python Toolset")

from tools.query_tools import register_query_tools
from tools.crud_tools import register_crud_tools
from tools.mep_tools import register_mep_tools
from tools.documentation_tools import register_documentation_tools
from tools.visualization_tools import register_visualization_tools
from tools.export_tools import register_export_tools
from tools.infrastructure_tools import register_infrastructure_tools
from resources.static_resources import register_static_resources
from resources.model_resources import register_model_resources
from prompts.toolset_prompts import register_toolset_prompts

register_query_tools(mcp)
register_crud_tools(mcp)
register_mep_tools(mcp)
register_documentation_tools(mcp)
register_visualization_tools(mcp)
register_export_tools(mcp)
register_infrastructure_tools(mcp)
register_static_resources(mcp)
register_model_resources(mcp)
register_toolset_prompts(mcp)
