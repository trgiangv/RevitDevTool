"""Infrastructure and document management DTOs."""
from __future__ import annotations

from pydantic import BaseModel, Field


class StatusResult(BaseModel):
    healthy: bool
    document_title: str | None = Field(default=None, alias="documentTitle")
    file_path: str | None = Field(default=None, alias="filePath")
    worksharing_enabled: bool | None = Field(default=None, alias="worksharingEnabled")
    central_path: str | None = Field(default=None, alias="centralPath")
    active_workset: str | None = Field(default=None, alias="activeWorkset")
    selection_count: int | None = Field(default=None, alias="selectionCount")
    version: str | None = None

    model_config = {"populate_by_name": True}


class SaveDocumentResult(BaseModel):
    saved: bool
    file_path: str = Field(alias="filePath")

    model_config = {"populate_by_name": True}


class CloseDocumentResult(BaseModel):
    closed: bool


class SyncResult(BaseModel):
    synced: bool


class GridAxisSpec(BaseModel):
    count: int
    spacing: float


class GenerateGridsResult(BaseModel):
    grid_ids: list[int]


class LevelSpec(BaseModel):
    name: str
    elevation: float
    create_view: bool = Field(default=False, alias="createView")

    model_config = {"populate_by_name": True}


class GenerateLevelsResult(BaseModel):
    level_ids: list[int]
