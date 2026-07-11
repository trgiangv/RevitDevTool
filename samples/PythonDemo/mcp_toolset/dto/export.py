"""Export result DTOs."""
from __future__ import annotations

from pydantic import BaseModel, Field


class ExportResult(BaseModel):
    file_path: str
    format: str = "xlsx"
    row_count: int
    column_count: int
    categories_exported: list[str] = Field(default_factory=list)
    file_size_bytes: int


class ScheduleExportResult(BaseModel):
    file_path: str
    schedule_name: str
    row_count: int
    column_count: int
    file_size_bytes: int


class ExportPdfResult(BaseModel):
    file_paths: list[str]
    page_count: int


class ExportImageResult(BaseModel):
    file_paths: list[str]
