"""Service for document save/close/sync operations."""

from Autodesk.Revit import DB

from dto.infrastructure import CloseDocumentResult, SaveDocumentResult, SyncResult
from shared.element_helpers import require_doc
from shared.path_guard import sanitize_file_path
from shared.responses import ToolError


class DocumentService:
    @staticmethod
    def close_document(save: bool = False) -> CloseDocumentResult:
        doc = require_doc()
        try:
            doc.Close(save)
            return CloseDocumentResult(closed=True)
        except Exception as exc:
            raise ToolError(f"Failed to close document: {exc}") from exc

    @staticmethod
    def save_document(file_path: str | None = None) -> SaveDocumentResult:
        doc = require_doc()
        try:
            if file_path:
                return _save_document_as(doc, file_path)
            return _save_document_in_place(doc)
        except ToolError:
            raise
        except Exception as exc:
            raise ToolError(f"Failed to save document: {exc}") from exc

    @staticmethod
    def sync_with_central(
        comment: str = "",
        compact: bool = False,
        relinquish_all: bool = False,
        save_local_before: bool = True,
    ) -> SyncResult:
        doc = require_doc()
        if not doc.IsWorkshared:
            raise ToolError(
                "Document is not workshared. Use revit_save_document instead."
            )

        try:
            sync_options = _build_sync_options(
                comment=comment,
                compact=compact,
                relinquish_all=relinquish_all,
                save_local_before=save_local_before,
            )
            doc.SynchronizeWithCentral(DB.TransactWithCentralOptions(), sync_options)
            return SyncResult(synced=True)
        except Exception as exc:
            raise ToolError(f"Failed to sync with central: {exc}") from exc


def _save_document_as(doc: DB.Document, file_path: str) -> SaveDocumentResult:
    target_path = sanitize_file_path(file_path)
    options = _build_save_as_options(doc)
    doc.SaveAs(target_path, options)
    return SaveDocumentResult(saved=True, filePath=target_path)


def _save_document_in_place(doc: DB.Document) -> SaveDocumentResult:
    doc.Save()
    return SaveDocumentResult(
        saved=True,
        filePath=(doc.PathName or ""),
    )


def _build_save_as_options(doc: DB.Document) -> DB.SaveAsOptions:
    options = DB.SaveAsOptions()
    options.OverwriteExistingFile = True
    if doc.IsWorkshared:
        ws_options = DB.WorksharingSaveAsOptions()
        ws_options.SaveAsCentral = True
        options.SetWorksharingOptions(ws_options)
    return options


def _build_sync_options(
    *,
    comment: str,
    compact: bool,
    relinquish_all: bool,
    save_local_before: bool,
) -> DB.SynchronizeWithCentralOptions:
    sync_options = DB.SynchronizeWithCentralOptions()
    sync_options.Comment = comment
    sync_options.Compact = compact
    sync_options.SaveLocalBefore = save_local_before
    if relinquish_all:
        sync_options.SetRelinquishOptions(DB.RelinquishOptions(True))
    return sync_options
