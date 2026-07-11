"""Service for document save/close/sync operations."""
from __future__ import annotations

from Autodesk.Revit import DB

from dto.infrastructure import CloseDocumentResult, SaveDocumentResult, SyncResult
from shared.element_helpers import normalize_string, require_doc
from shared.path_guard import sanitize_file_path
from shared.responses import ToolError


class DocumentService:
    def close_document(self, save: bool = False) -> CloseDocumentResult:
        doc = require_doc()
        try:
            doc.Close(save)
            return CloseDocumentResult(closed=True)
        except Exception as exc:
            raise ToolError("Failed to close document: {}".format(exc)) from exc

    def save_document(self, file_path: str | None = None) -> SaveDocumentResult:
        doc = require_doc()
        try:
            if file_path:
                target_path = sanitize_file_path(file_path)
                options = DB.SaveAsOptions()
                options.OverwriteExistingFile = True
                if doc.IsWorkshared:
                    ws_options = DB.WorksharingSaveAsOptions()
                    ws_options.SaveAsCentral = True
                    options.SetWorksharingOptions(ws_options)
                doc.SaveAs(target_path, options)
                return SaveDocumentResult(saved=True, filePath=target_path)

            doc.Save()
            return SaveDocumentResult(saved=True, filePath=normalize_string(doc.PathName))
        except ToolError:
            raise
        except Exception as exc:
            raise ToolError("Failed to save document: {}".format(exc)) from exc

    def sync_with_central(
        self,
        comment: str = "",
        compact: bool = False,
        relinquish_all: bool = False,
        save_local_before: bool = True,
    ) -> SyncResult:
        doc = require_doc()
        if not doc.IsWorkshared:
            raise ToolError("Document is not workshared. Use revit_save_document instead.")

        try:
            transact_options = DB.TransactWithCentralOptions()
            sync_options = DB.SynchronizeWithCentralOptions()
            sync_options.Comment = comment
            sync_options.Compact = compact
            sync_options.SaveLocalBefore = save_local_before
            if relinquish_all:
                sync_options.SetRelinquishOptions(DB.RelinquishOptions(True))

            doc.SynchronizeWithCentral(transact_options, sync_options)
            return SyncResult(synced=True)
        except Exception as exc:
            raise ToolError("Failed to sync with central: {}".format(exc)) from exc
