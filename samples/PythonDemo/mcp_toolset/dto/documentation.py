"""Documentation (views, sheets, schedules) DTOs."""

from pydantic import BaseModel, Field


class CreateViewResult(BaseModel):
    view_id: int = Field(alias="viewId")
    view_name: str = Field(alias="viewName")

    model_config = {"populate_by_name": True}


class CreateSheetResult(BaseModel):
    sheet_id: int = Field(alias="sheetId")
    sheet_number: str = Field(alias="sheetNumber")

    model_config = {"populate_by_name": True}


class PlaceOnSheetResult(BaseModel):
    viewport_id: int = Field(alias="viewportId")

    model_config = {"populate_by_name": True}


class SortRule(BaseModel):
    field: str
    ascending: bool = True


class FilterRule(BaseModel):
    field: str
    operator: str
    value: str
    is_numeric: bool = Field(default=False, alias="isNumeric")

    model_config = {"populate_by_name": True}


class GroupRule(BaseModel):
    field: str
    show_header: bool = Field(default=True, alias="showHeader")
    show_footer: bool = Field(default=True, alias="showFooter")

    model_config = {"populate_by_name": True}


class ScheduleConfig(BaseModel):
    category_name: str = Field(alias="categoryName")
    schedule_name: str | None = Field(default=None, alias="scheduleName")
    fields: list[str] = Field(default_factory=list)
    sort_rules: list[SortRule] = Field(default_factory=list, alias="sortRules")
    filter_rules: list[FilterRule] = Field(default_factory=list, alias="filterRules")
    group_rules: list[GroupRule] = Field(default_factory=list, alias="groupRules")

    model_config = {"populate_by_name": True}


class CreateScheduleResult(BaseModel):
    schedule_id: int = Field(alias="scheduleId")
    schedule_name: str = Field(alias="scheduleName")

    model_config = {"populate_by_name": True}


class ApplyViewTemplateResult(BaseModel):
    applied: bool


class ViewItem(BaseModel):
    id: int
    name: str
    view_type: str = Field(alias="viewType")
    is_sheet: bool = Field(alias="isSheet")
    sheet_number: str | None = Field(default=None, alias="sheetNumber")
    level: str | None = None
    template: str | None = None
    on_sheet: bool = Field(alias="onSheet")
    sheet_ids: list[int] = Field(default_factory=list, alias="sheetIds")

    model_config = {"populate_by_name": True}


class ListViewsResult(BaseModel):
    views: list[ViewItem]


class ScheduleFieldInfo(BaseModel):
    name: str
    field_type: str = Field(alias="fieldType")

    model_config = {"populate_by_name": True}


class ListScheduleFieldsResult(BaseModel):
    fields: list[ScheduleFieldInfo]


class ActivateViewResult(BaseModel):
    activated: bool
    view_name: str = Field(alias="viewName")

    model_config = {"populate_by_name": True}
