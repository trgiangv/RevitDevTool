"""Service for health/status information."""

from __future__ import annotations

from shared.context import get_doc
from shared.element_helpers import normalize_string


class StatusService:
    def get_status(self) -> dict:
        try:
            doc = get_doc()
            if doc is None:
                return {
                    "health": "no_document",
                    "revit_available": False,
                    "api_name": "revitdevtool",
                }

            return {
                "health": "healthy",
                "revit_available": True,
                "document_title": normalize_string(doc.Title),
                "api_name": "revitdevtool",
            }
        except Exception as exc:
            return {
                "health": "error",
                "revit_available": False,
                "api_name": "revitdevtool",
                "error": str(exc),
            }
