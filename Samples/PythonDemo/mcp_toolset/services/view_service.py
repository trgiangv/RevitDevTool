"""Service for view-centric queries and exports."""

from __future__ import annotations

import base64
import os
import tempfile

from System.Collections.Generic import List
from Autodesk.Revit import DB
from RevitDevTool.Core import RevitContext

from dto.views import (
    ViewElementInfo,
    ViewElementsResult,
    ViewImageResult,
    ViewInfo,
    ViewInfoResult,
    ViewListResult,
)
from shared.element_helpers import category_display_name, element_id_value, normalize_string, require_doc
from shared.responses import ToolError


class ViewService:
    _view_type_to_bucket = {
        "FloorPlan": "floor_plans",
        "CeilingPlan": "ceiling_plans",
        "Elevation": "elevations",
        "Section": "sections",
        "ThreeD": "3d_views",
        "DraftingView": "drafting_views",
        "Schedule": "schedules",
    }

    def _require_active_view(self) -> tuple[DB.UIDocument, DB.View]:
        uidoc = RevitContext.ActiveUiDocument
        if uidoc is None or uidoc.Document is None:
            raise ToolError("No active Revit document")
        current_view = uidoc.ActiveView
        if current_view is None:
            raise ToolError("No active view found")
        return uidoc, current_view

    def list_views(self) -> ViewListResult:
        doc = require_doc()

        views_by_type = {
            "floor_plans": [],
            "ceiling_plans": [],
            "elevations": [],
            "sections": [],
            "3d_views": [],
            "drafting_views": [],
            "schedules": [],
            "other": [],
        }

        all_views = DB.FilteredElementCollector(doc).OfClass(DB.View).ToElements()
        for view in all_views:
            if self._skip_view(view):
                continue
            try:
                view_name = normalize_string(view.Name)
                view_type_name = str(view.ViewType)
                bucket = self._view_type_to_bucket.get(view_type_name, "other")
                views_by_type[bucket].append(view_name)
            except Exception:
                continue

        for bucket in views_by_type.values():
            bucket.sort()
        total_views = sum(len(bucket) for bucket in views_by_type.values())
        return ViewListResult(views_by_type=views_by_type, total_exportable_views=total_views)

    @staticmethod
    def _skip_view(view: DB.View) -> bool:
        try:
            if view.IsTemplate:
                return True
            return view.ViewType in (DB.ViewType.Internal, DB.ViewType.ProjectBrowser)
        except Exception:
            return True

    def current_view_info(self) -> ViewInfoResult:
        _, current_view = self._require_active_view()

        scale = None
        try:
            scale = int(current_view.Scale)
        except Exception:
            pass

        crop_box_active = False
        try:
            crop_box_active = bool(current_view.CropBoxActive)
        except Exception:
            pass

        detail_level = "Unknown"
        try:
            detail_level = str(current_view.DetailLevel)
        except Exception:
            pass

        discipline = "Unknown"
        try:
            discipline = str(current_view.Discipline)
        except Exception:
            pass

        view_family_type = "Unknown"
        try:
            type_elem = current_view.Document.GetElement(current_view.GetTypeId())
            if type_elem is not None:
                view_family_type = normalize_string(type_elem.Name)
        except Exception:
            pass

        view_info = ViewInfo(
            view_name=normalize_string(current_view.Name),
            view_type=str(current_view.ViewType),
            view_id=element_id_value(current_view.Id),
            is_template=bool(current_view.IsTemplate),
            scale=scale,
            crop_box_active=crop_box_active,
            detail_level=detail_level,
            discipline=discipline,
            view_family_type=view_family_type,
        )
        return ViewInfoResult(view_info=view_info)

    def current_view_elements(
        self, limit: int = 5000, include_levels: bool = False, include_location: bool = False
    ) -> ViewElementsResult:
        doc = require_doc()
        uidoc = RevitContext.ActiveUiDocument
        if uidoc is None:
            raise ToolError("No active Revit document")

        current_view = uidoc.ActiveView
        if current_view is None:
            raise ToolError("No active view found")

        collector = DB.FilteredElementCollector(doc, current_view.Id)
        elements = collector.WhereElementIsNotElementType().ToElements()
        level_cache = {}
        elements_info = []
        category_counts = {}
        total_elements = 0

        for elem in elements:
            try:
                cat = elem.Category
                cat_name = category_display_name(elem)
                category_counts[cat_name] = category_counts.get(cat_name, 0) + 1
                total_elements += 1
                if len(elements_info) >= int(limit):
                    continue
                item = self._build_element_info(doc, elem, cat, include_levels, include_location, level_cache)
                elements_info.append(item)
            except Exception:
                continue

        return ViewElementsResult(
            current_view=normalize_string(current_view.Name),
            total_elements=total_elements,
            returned_elements=len(elements_info),
            truncated=total_elements > len(elements_info),
            category_counts=category_counts,
            elements=elements_info,
        )

    def _build_element_info(
        self,
        doc: DB.Document,
        elem: DB.Element,
        cat: DB.Category | None,
        include_levels: bool,
        include_location: bool,
        level_cache: dict,
    ) -> ViewElementInfo:
        info = {
            "element_id": element_id_value(elem.Id),
            "name": normalize_string(elem.Name),
            "category": category_display_name(elem),
            "category_id": element_id_value(cat.Id) if cat else None,
            "level": None,
            "level_id": None,
            "location": None,
        }
        if include_levels:
            self._attach_level_info(doc, elem, info, level_cache)
        if include_location:
            self._attach_location_info(elem, info)
        return ViewElementInfo(**info)

    def _attach_level_info(self, doc: DB.Document, elem: DB.Element, info: dict, level_cache: dict) -> None:
        try:
            level_param = elem.get_Parameter(DB.BuiltInParameter.FAMILY_LEVEL_PARAM)
            if not level_param:
                return
            level_id = level_param.AsElementId()
            if level_id == DB.ElementId.InvalidElementId:
                info["level"] = None
                info["level_id"] = None
                return
            lid = element_id_value(level_id)
            if lid not in level_cache:
                level_elem = doc.GetElement(level_id)
                level_cache[lid] = {"name": normalize_string(level_elem.Name) if level_elem else None, "id": lid}
            info["level"] = level_cache[lid]["name"]
            info["level_id"] = level_cache[lid]["id"]
        except Exception:
            info["level"] = None
            info["level_id"] = None

    @staticmethod
    def _attach_location_info(elem: DB.Element, info: dict) -> None:
        try:
            location = elem.Location
            if isinstance(location, DB.LocationPoint):
                pt = location.Point
                info["location"] = {"type": "point", "x": pt.X, "y": pt.Y, "z": pt.Z}
                return
            if isinstance(location, DB.LocationCurve):
                curve = location.Curve
                start = curve.GetEndPoint(0)
                end = curve.GetEndPoint(1)
                info["location"] = {
                    "type": "curve",
                    "start": {"x": start.X, "y": start.Y, "z": start.Z},
                    "end": {"x": end.X, "y": end.Y, "z": end.Z},
                }
                return
        except Exception:
            pass
        info["location"] = {"type": "unknown"}

    def get_view_image(self, view_name: str) -> ViewImageResult:
        doc = require_doc()

        normalized_name = normalize_string(view_name)
        target_view = self._find_view_by_name(doc, normalized_name)
        if target_view is None:
            raise ToolError("View '{}' not found".format(normalized_name), code="revit.view_not_found")

        output_folder = os.path.join(tempfile.gettempdir(), "RevitMCPExports")
        if not os.path.exists(output_folder):
            os.makedirs(output_folder)
        file_path_prefix = os.path.join(output_folder, "export")

        options = DB.ImageExportOptions()
        options.ExportRange = DB.ExportRange.SetOfViews
        view_ids = List[DB.ElementId]()
        view_ids.Add(target_view.Id)
        options.SetViewsAndSheets(view_ids)
        options.FilePath = file_path_prefix
        options.HLRandWFViewsFileType = DB.ImageFileType.PNG
        options.ShadowViewsFileType = DB.ImageFileType.PNG
        options.ImageResolution = DB.ImageResolution.DPI_150
        options.ZoomType = DB.ZoomFitType.FitToPage
        options.PixelSize = 1024
        doc.ExportImage(options)

        exported_file = self._find_latest_png(output_folder)
        if not exported_file:
            raise ToolError("Export failed - no image file was created", code="revit.image_export_failed")

        try:
            with open(exported_file, "rb") as img_file:
                img_data = img_file.read()
            return ViewImageResult(
                image_data=base64.b64encode(img_data).decode("utf-8"),
                content_type="image/png",
                view_name=normalized_name,
                file_size_bytes=len(img_data),
            )
        finally:
            if os.path.exists(exported_file):
                os.remove(exported_file)

    @staticmethod
    def _find_view_by_name(doc: DB.Document, view_name: str) -> DB.View | None:
        all_views = DB.FilteredElementCollector(doc).OfClass(DB.View).ToElements()
        for view in all_views:
            try:
                if normalize_string(view.Name) == view_name:
                    return view
            except Exception:
                continue
        return None

    @staticmethod
    def _find_latest_png(folder: str) -> str | None:
        files = [os.path.join(folder, name) for name in os.listdir(folder) if name.endswith(".png")]
        files.sort(key=lambda path: os.path.getctime(path), reverse=True)
        return files[0] if files else None
