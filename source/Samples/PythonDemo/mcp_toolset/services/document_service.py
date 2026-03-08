"""Service for document open/save/close/sync operations."""

from __future__ import annotations

import os

from shared.context import get_doc, get_uiapp
from shared.element_helpers import normalize_string
from shared.responses import ToolError


class DocumentService:
    def open_document(self, file_path: str, detach: bool = False, audit: bool = False) -> dict:
        from Autodesk.Revit import DB

        if not file_path:
            raise ToolError("No file_path provided")
        if not os.path.isfile(file_path):
            raise ToolError("File not found: {}".format(file_path))

        uiapp = get_uiapp()
        model_path = DB.ModelPathUtils.ConvertUserVisiblePathToModelPath(file_path)
        open_options = DB.OpenOptions()
        if detach:
            open_options.DetachFromCentralOption = DB.DetachFromCentralOption.DetachAndPreserveWorksets
        if audit:
            open_options.Audit = True

        uiapp.OpenAndActivateDocument(model_path, open_options, False)
        doc = get_doc()
        result = {
            "file_path": file_path,
            "document_title": normalize_string(doc.Title) if doc is not None else os.path.basename(file_path),
            "is_workshared": bool(doc.IsWorkshared) if doc is not None else None,
        }
        if detach:
            result["detached"] = True
        return result

    def close_document(self, save: bool = False) -> dict:
        from Autodesk.Revit.UI import PostableCommand, RevitCommandId

        doc = get_doc()
        if doc is None:
            raise ToolError("No active document to close")

        doc_title = normalize_string(doc.Title)
        if save:
            try:
                doc.Save()
            except Exception:
                pass
        uiapp = get_uiapp()
        close_cmd = RevitCommandId.LookupPostableCommandId(PostableCommand.Close)
        uiapp.PostCommand(close_cmd)
        return {
            "document_title": doc_title,
            "saved": save,
        }

    def save_document(self, file_path: str = None) -> dict:
        from Autodesk.Revit import DB

        doc = get_doc()
        if doc is None:
            raise ToolError("No active document to save")

        doc_title = normalize_string(doc.Title)
        if file_path:
            options = DB.SaveAsOptions()
            options.OverwriteExistingFile = True
            if doc.IsWorkshared:
                ws_options = DB.WorksharingSaveAsOptions()
                ws_options.SaveAsCentral = True
                options.SetWorksharingOptions(ws_options)
            doc.SaveAs(file_path, options)
            return {
                "document_title": doc_title,
                "saved_path": file_path,
                "save_type": "save_as",
            }
        doc.Save()
        return {
            "document_title": doc_title,
            "save_type": "save",
        }

    def sync_with_central(self, comment: str = "", compact: bool = False, relinquish_all: bool = True) -> dict:
        from Autodesk.Revit import DB

        doc = get_doc()
        if doc is None:
            raise ToolError("No active document")
        if not doc.IsWorkshared:
            raise ToolError("Document is not workshared. Use save_document instead.")

        doc_title = normalize_string(doc.Title)
        transact_options = DB.TransactWithCentralOptions()
        sync_options = DB.SynchronizeWithCentralOptions()
        sync_options.Comment = comment
        sync_options.Compact = compact
        if relinquish_all:
            sync_options.SetRelinquishOptions(DB.RelinquishOptions(True))

        doc.SynchronizeWithCentral(transact_options, sync_options)
        return {
            "document_title": doc_title,
            "comment": comment,
            "compacted": compact,
            "relinquished_all": relinquish_all,
        }
