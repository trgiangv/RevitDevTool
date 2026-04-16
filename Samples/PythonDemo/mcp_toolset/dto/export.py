from __future__ import annotations
from pydantic import BaseModel, Field


class ExportResult(BaseModel):
    file_path: str = Field(description="Absolute path to the exported file")
    format: str = Field(description="Export format: 'xlsx' or 'csv'")
    row_count: int = Field(description="Number of data rows exported")
    column_count: int = Field(description="Number of columns exported")
    categories_exported: list[str] = Field(description="Revit categories included in the export")
    file_size_bytes: int = Field(description="Size of the exported file in bytes")


class ScheduleExportResult(BaseModel):
    file_path: str
    schedule_name: str
    row_count: int
    column_count: int
    file_size_bytes: int
