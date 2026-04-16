#load "../core/RevitContext.fsx"

namespace Demo

module DocumentSummary =
    open Autodesk.Revit.UI

    let buildSummary (commandData: ExternalCommandData) =
        let title = RevitContext.getDocumentTitle commandData
        [
            "F# modular script demo"
            $"Document: {title}"
        ]
