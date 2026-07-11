"""Documentation tools: views, sheets, schedules."""
from __future__ import annotations

import math
from datetime import datetime

from Autodesk.Revit import DB
from RevitDevTool.Core import RevitContext

from dto.documentation import (
    ActivateViewResult,
    ApplyViewTemplateResult,
    CreateScheduleResult,
    CreateSheetResult,
    CreateViewResult,
    ListScheduleFieldsResult,
    ListViewsResult,
    PlaceOnSheetResult,
    ScheduleConfig,
    ScheduleFieldInfo,
    ViewItem,
)
from shared.element_helpers import element_id_value, find_category_by_name, normalize_string, require_doc
from shared.responses import ToolError
from shared.transactions import run_transaction


class DocumentationService:
    def create_view(
        self,
        view_type: str,
        level_name: str | None = None,
        view_name: str | None = None,
        template_name: str | None = None,
        min_point: list[float] | None = None,
        max_point: list[float] | None = None,
        direction_angle: float | None = None,
        depth: float | None = None,
        is_bounding_box: bool | None = None,
    ) -> CreateViewResult:
        doc = require_doc()
        normalized = view_type.strip().lower().replace("-", "_")

        def _operation():
            if normalized == "floor_plan":
                return self._create_floor_plan(doc, level_name, view_name, template_name)
            if normalized == "section":
                return self._create_section(doc, min_point, max_point, direction_angle, depth, view_name)
            if normalized in ("3d", "three_d"):
                return self._create_3d(doc, view_name, template_name, is_bounding_box)
            raise ToolError("Unsupported view type '{}'. Use floor_plan, section, or 3d".format(view_type))

        view = run_transaction(doc, "MCP: revit_create_view", _operation)
        return CreateViewResult(viewId=element_id_value(view.Id), viewName=normalize_string(view.Name))

    def create_sheet(self, title_block_id: int | None = None) -> CreateSheetResult:
        doc = require_doc()
        tb_id = DB.ElementId(title_block_id) if title_block_id else self._default_title_block(doc)

        def _operation():
            return DB.ViewSheet.Create(doc, tb_id)

        sheet = run_transaction(doc, "MCP: revit_create_sheet", _operation)
        return CreateSheetResult(
            sheetId=element_id_value(sheet.Id),
            sheetNumber=normalize_string(sheet.SheetNumber),
        )

    def place_on_sheet(
        self,
        sheet_id: int,
        view_or_schedule_id: int,
        position: list[float] | None = None,
    ) -> PlaceOnSheetResult:
        doc = require_doc()
        sheet = doc.GetElement(DB.ElementId(sheet_id))
        if not isinstance(sheet, DB.ViewSheet):
            raise ToolError("Sheet {} not found".format(sheet_id))
        element = doc.GetElement(DB.ElementId(view_or_schedule_id))
        if element is None:
            raise ToolError("Element {} not found".format(view_or_schedule_id))
        placement = DB.XYZ(position[0], position[1], 0.0) if position and len(position) >= 2 else DB.XYZ.Zero

        def _operation():
            if isinstance(element, DB.ViewSchedule):
                instance = DB.ScheduleSheetInstance.Create(doc, sheet.Id, element.Id, placement)
                return instance
            if isinstance(element, DB.View):
                if element.IsTemplate:
                    raise ToolError("Cannot place a template view on a sheet")
                return DB.Viewport.Create(doc, sheet.Id, element.Id, placement)
            raise ToolError("Element {} is not a view or schedule".format(view_or_schedule_id))

        viewport = run_transaction(doc, "MCP: revit_place_on_sheet", _operation)
        ui_doc = RevitContext.ActiveUiDocument
        if ui_doc is not None:
            ui_doc.RequestViewChange(sheet)
        return PlaceOnSheetResult(viewportId=element_id_value(viewport.Id))

    def create_schedule(self, config: ScheduleConfig) -> CreateScheduleResult:
        doc = require_doc()
        category = find_category_by_name(doc, config.category_name)
        if category is None:
            raise ToolError("Category '{}' not found".format(config.category_name))

        def _operation():
            schedule = DB.ViewSchedule.CreateSchedule(doc, category.Id)
            schedule.Name = config.schedule_name or "{} Schedule {}".format(
                config.category_name, datetime.now().strftime("%Y-%m-%d_%H-%M-%S")
            )
            schedulable = list(schedule.Definition.GetSchedulableFields())
            for field_name in config.fields:
                for sf in schedulable:
                    if sf.GetName(doc).lower() == field_name.lower():
                        schedule.Definition.AddField(sf)
                        break
            for sort_rule in config.sort_rules:
                field = self._find_schedule_field(schedule, sort_rule.field)
                if field is None:
                    raise ToolError("Sort field '{}' not found".format(sort_rule.field))
                direction = DB.ScheduleSortOrder.Ascending if sort_rule.ascending else DB.ScheduleSortOrder.Descending
                schedule.Definition.AddSortGroupField(DB.ScheduleSortGroupField(field.FieldId, direction))
            return schedule

        schedule = run_transaction(doc, "MCP: revit_create_schedule", _operation)
        return CreateScheduleResult(
            scheduleId=element_id_value(schedule.Id),
            scheduleName=normalize_string(schedule.Name),
        )

    def apply_view_template(self, view_id: int, template_name: str | None = None) -> ApplyViewTemplateResult:
        doc = require_doc()
        view = doc.GetElement(DB.ElementId(view_id))
        if not isinstance(view, DB.View):
            raise ToolError("View {} not found".format(view_id))
        if view.IsTemplate:
            raise ToolError("Cannot apply a template to a template view")

        def _operation():
            if not template_name:
                view.ViewTemplateId = DB.ElementId.InvalidElementId
                return False
            template = self._find_view_template(doc, template_name, view.ViewType)
            if template is None:
                raise ToolError("View template '{}' not found".format(template_name))
            view.ViewTemplateId = template.Id
            return True

        applied = run_transaction(doc, "MCP: revit_apply_view_template", _operation)
        return ApplyViewTemplateResult(applied=applied)

    def list_views(
        self,
        include_sheets: bool = True,
        include_templates: bool = False,
    ) -> ListViewsResult:
        doc = require_doc()
        on_sheet = self._views_on_sheets_map(doc)
        views = []
        for view in DB.FilteredElementCollector(doc).OfClass(DB.View).ToElements():
            if view.IsTemplate and not include_templates:
                continue
            if isinstance(view, DB.ViewSheet) and not include_sheets:
                continue
            if view.ViewType in (DB.ViewType.Internal, DB.ViewType.ProjectBrowser):
                continue
            template_name = None
            if view.ViewTemplateId and view.ViewTemplateId != DB.ElementId.InvalidElementId:
                tmpl = doc.GetElement(view.ViewTemplateId)
                if tmpl:
                    template_name = normalize_string(tmpl.Name)
            vid = element_id_value(view.Id)
            sheet_ids = on_sheet.get(vid, [])
            level_name = None
            if view.GenLevel:
                level_name = normalize_string(view.GenLevel.Name)
            views.append(
                ViewItem(
                    id=vid,
                    name=normalize_string(view.Name),
                    viewType=str(view.ViewType),
                    isSheet=isinstance(view, DB.ViewSheet),
                    sheetNumber=view.SheetNumber if isinstance(view, DB.ViewSheet) else None,
                    level=level_name,
                    template=template_name,
                    onSheet=len(sheet_ids) > 0,
                    sheetIds=sheet_ids,
                )
            )
        views.sort(key=lambda item: item.name.lower())
        return ListViewsResult(views=views)

    def list_schedule_fields(self, category_name: str) -> ListScheduleFieldsResult:
        doc = require_doc()
        category = find_category_by_name(doc, category_name)
        if category is None:
            raise ToolError("Category '{}' not found".format(category_name))

        def _operation():
            schedule = DB.ViewSchedule.CreateSchedule(doc, category.Id)
            fields = []
            for sf in schedule.Definition.GetSchedulableFields():
                fields.append(
                    ScheduleFieldInfo(name=sf.GetName(doc), fieldType=str(sf.FieldType))
                )
            fields.sort(key=lambda item: item.name.lower())
            return fields

        fields = run_transaction(doc, "Temporary Schedule for Field Discovery", _operation)
        return ListScheduleFieldsResult(fields=fields)

    def activate_view(self, view_id: int) -> ActivateViewResult:
        doc = require_doc()
        view = doc.GetElement(DB.ElementId(view_id))
        if not isinstance(view, DB.View):
            raise ToolError("View {} not found".format(view_id))
        ui_doc = RevitContext.ActiveUiDocument
        if ui_doc is None:
            raise ToolError("No active UI document")
        ui_doc.RequestViewChange(view)
        return ActivateViewResult(activated=True, viewName=normalize_string(view.Name))

    def _create_floor_plan(
        self,
        doc: DB.Document,
        level_name: str | None,
        view_name: str | None,
        template_name: str | None,
    ) -> DB.View:
        if not level_name:
            raise ToolError("levelName is required for floor_plan")
        level = None
        for lvl in DB.FilteredElementCollector(doc).OfClass(DB.Level).ToElements():
            if normalize_string(lvl.Name) == normalize_string(level_name):
                level = lvl
                break
        if level is None:
            raise ToolError("Level '{}' not found".format(level_name))
        vft = self._floor_plan_type(doc)
        view = DB.ViewPlan.Create(doc, vft.Id, level.Id)
        if view_name:
            view.Name = view_name
        if template_name:
            tmpl = self._find_view_template(doc, template_name, view.ViewType)
            if tmpl:
                view.ViewTemplateId = tmpl.Id
        return view

    def _create_section(
        self,
        doc: DB.Document,
        min_point: list[float] | None,
        max_point: list[float] | None,
        direction_angle: float | None,
        depth: float | None,
        view_name: str | None,
    ) -> DB.View:
        if not min_point or not max_point or len(min_point) < 3 or len(max_point) < 3:
            raise ToolError("Section requires min and max bounding box points")
        min_pt = DB.XYZ(min_point[0], min_point[1], min_point[2])
        max_pt = DB.XYZ(max_point[0], max_point[1], max_point[2])
        bbox = DB.BoundingBoxXYZ()
        bbox.Min = min_pt
        bbox.Max = max_pt
        angle = (direction_angle or 0.0) * math.pi / 180.0
        vft = self._section_type(doc)
        view = DB.ViewSection.CreateSection(doc, vft.Id, bbox)
        if view_name:
            view.Name = view_name
        return view

    def _create_3d(
        self,
        doc: DB.Document,
        view_name: str | None,
        template_name: str | None,
        is_bounding_box: bool | None,
    ) -> DB.View:
        vft = self._3d_type(doc)
        if is_bounding_box:
            view = DB.View3D.CreateIsometric(doc, vft.Id)
        else:
            view = DB.View3D.CreateIsometric(doc, vft.Id)
        if view_name:
            view.Name = view_name
        if template_name:
            tmpl = self._find_view_template(doc, template_name, view.ViewType)
            if tmpl:
                view.ViewTemplateId = tmpl.Id
        return view

    @staticmethod
    def _floor_plan_type(doc: DB.Document) -> DB.ViewFamilyType:
        for vft in DB.FilteredElementCollector(doc).OfClass(DB.ViewFamilyType).ToElements():
            if vft.ViewFamily == DB.ViewFamily.FloorPlan:
                return vft
        raise ToolError("No floor plan view family type found")

    @staticmethod
    def _section_type(doc: DB.Document) -> DB.ViewFamilyType:
        for vft in DB.FilteredElementCollector(doc).OfClass(DB.ViewFamilyType).ToElements():
            if vft.ViewFamily == DB.ViewFamily.Section:
                return vft
        raise ToolError("No section view family type found")

    @staticmethod
    def _3d_type(doc: DB.Document) -> DB.ViewFamilyType:
        for vft in DB.FilteredElementCollector(doc).OfClass(DB.ViewFamilyType).ToElements():
            if vft.ViewFamily == DB.ViewFamily.ThreeDimensional:
                return vft
        raise ToolError("No 3D view family type found")

    @staticmethod
    def _default_title_block(doc: DB.Document) -> DB.ElementId:
        symbol = (
            DB.FilteredElementCollector(doc)
            .OfCategory(DB.BuiltInCategory.OST_TitleBlocks)
            .WhereElementIsElementType()
            .FirstElement()
        )
        if symbol is None:
            raise ToolError("No title block found in document")
        return symbol.Id

    @staticmethod
    def _find_view_template(doc: DB.Document, name: str, view_type: DB.ViewType) -> DB.View | None:
        target = normalize_string(name)
        for view in DB.FilteredElementCollector(doc).OfClass(DB.View).ToElements():
            if view.IsTemplate and normalize_string(view.Name) == target:
                return view
        return None

    @staticmethod
    def _find_schedule_field(schedule: DB.ViewSchedule, field_name: str) -> DB.ScheduleField | None:
        definition = schedule.Definition
        for i in range(definition.GetFieldCount()):
            field = definition.GetField(i)
            if field.GetName().lower() == field_name.lower():
                return field
        return None

    @staticmethod
    def _views_on_sheets_map(doc: DB.Document) -> dict[int, list[int]]:
        mapping: dict[int, list[int]] = {}
        for vp in DB.FilteredElementCollector(doc).OfClass(DB.Viewport).ToElements():
            vid = element_id_value(vp.ViewId)
            sid = element_id_value(vp.OwnerViewId)
            mapping.setdefault(vid, []).append(sid)
        for inst in DB.FilteredElementCollector(doc).OfClass(DB.ScheduleSheetInstance).ToElements():
            vid = element_id_value(inst.ScheduleId)
            sid = element_id_value(inst.OwnerViewId)
            mapping.setdefault(vid, []).append(sid)
        return mapping
