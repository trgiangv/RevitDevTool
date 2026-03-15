from __future__ import annotations
from pydantic import BaseModel, Field


class OpenDocumentResult(BaseModel):
    file_path: str
    document_title: str
    is_workshared: bool | None = None
    detached: bool = False


class CloseDocumentResult(BaseModel):
    document_title: str
    saved: bool


class SaveDocumentResult(BaseModel):
    document_title: str
    saved_path: str | None = None
    save_type: str = Field(description="'save' or 'save_as'")


class SyncResult(BaseModel):
    document_title: str
    comment: str
    compacted: bool
    relinquished_all: bool
