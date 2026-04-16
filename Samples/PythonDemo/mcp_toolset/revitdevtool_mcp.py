# /// script
# dependencies = [
#     "polars",
#     "xlsxwriter",
# ]
# ///
from mcp.server.fastmcp import FastMCP

mcp = FastMCP("Revit Python Toolset")

from tools.status_tools import register_status_tools
from tools.model_tools import register_model_tools
from tools.view_tools import register_view_tools
from tools.family_tools import register_family_tools
from tools.document_tools import register_document_tools
from tools.colors_tools import register_colors_tools
from tools.code_execution_tools import register_code_execution_tools
from tools.launch_tools import register_launch_tools
from tools.export_tools import register_export_tools

register_status_tools(mcp)
register_model_tools(mcp)
register_view_tools(mcp)
register_family_tools(mcp)
register_document_tools(mcp)
register_colors_tools(mcp)
register_code_execution_tools(mcp)
register_launch_tools(mcp)
register_export_tools(mcp)
