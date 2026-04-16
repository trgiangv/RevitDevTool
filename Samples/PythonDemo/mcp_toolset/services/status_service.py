"""Service for health/status information."""
from __future__ import annotations

from RevitDevTool.Core import RevitContext

from dto.status import StatusResult
from shared.element_helpers import normalize_string


class StatusService:
    def get_status(self) -> StatusResult:
        try:
            doc = RevitContext.ActiveDocument
            if doc is None:
                return StatusResult(
                    health="no_document",
                    revit_available=False,
                )

            return StatusResult(
                health="healthy",
                revit_available=True,
                document_title=normalize_string(doc.Title),
            )
        except Exception as exc:
            return StatusResult(
                health="error",
                revit_available=False,
                error=str(exc),
            )
