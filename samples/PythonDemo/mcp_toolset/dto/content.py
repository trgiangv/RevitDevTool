"""DTOs for multimodal content tools."""

from pydantic import BaseModel, Field


class ViewCaptureResult(BaseModel):
    data: bytes
    view_name: str = Field(alias="viewName")
    view_id: int = Field(alias="viewId")
    image_path: str = Field(alias="imagePath")

    model_config = {"populate_by_name": True}


class SchedulePreviewResult(BaseModel):
    schedule_name: str = Field(alias="scheduleName")
    csv_text: str = Field(alias="csvText")
    embedded_rows: int = Field(alias="embeddedRows")
    total_rows: int = Field(alias="totalRows")
    column_count: int = Field(alias="columnCount")

    model_config = {"populate_by_name": True}


class ModelDigestResult(BaseModel):
    project_title: str = Field(alias="projectTitle")
    view_count: int = Field(alias="viewCount")
    level_count: int = Field(alias="levelCount")
    warning_count: int = Field(alias="warningCount")

    model_config = {"populate_by_name": True}
