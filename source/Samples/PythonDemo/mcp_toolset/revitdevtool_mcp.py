from __future__ import annotations

import sys
from pathlib import Path

from mcp.server.fastmcp import FastMCP


CURRENT_DIR = Path(__file__).resolve().parent
if str(CURRENT_DIR) not in sys.path:
    sys.path.insert(0, str(CURRENT_DIR))

mcp = FastMCP("Revit Python Toolset")

from services.code_execution_service import CodeExecutionService
from services.colors_service import ColorsService
from services.document_service import DocumentService
from services.family_service import FamilyService
from services.launch_service import LaunchService
from services.model_service import ModelService
from services.status_service import StatusService
from services.view_service import ViewService
from tools.code_execution_tools import register_code_execution_tools
from tools.colors_tools import register_colors_tools
from tools.document_tools import register_document_tools
from tools.family_tools import register_family_tools
from tools.launch_tools import register_launch_tools
from tools.model_tools import register_model_tools
from tools.status_tools import register_status_tools
from tools.view_tools import register_view_tools

status_service = StatusService()
model_service = ModelService()
view_service = ViewService()
family_service = FamilyService()
document_service = DocumentService()
colors_service = ColorsService()
code_execution_service = CodeExecutionService()
launch_service = LaunchService(status_service=status_service)

register_status_tools(mcp, status_service=status_service, model_service=model_service)
register_view_tools(mcp, view_service=view_service)
register_model_tools(mcp, model_service=model_service)
register_family_tools(mcp, family_service=family_service)
register_document_tools(mcp, document_service=document_service)
register_colors_tools(mcp, colors_service=colors_service)
register_code_execution_tools(mcp, code_execution_service=code_execution_service)
register_launch_tools(mcp, launch_service=launch_service)