from __future__ import annotations
from pydantic import BaseModel, Field


class StatusResult(BaseModel):
    health: str = Field(description="Health status: 'healthy', 'no_document', or 'error'")
    revit_available: bool = Field(description="Whether Revit API is accessible")
    document_title: str | None = Field(default=None, description="Title of the active document, if any")
    api_name: str = Field(default="revitdevtool", description="API identifier")
    error: str | None = Field(default=None, description="Error message if health is 'error'")
