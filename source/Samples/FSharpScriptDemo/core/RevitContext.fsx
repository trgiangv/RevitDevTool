#r "C:/Program Files/Autodesk/Revit 2024/RevitAPI.dll"
#r "C:/Program Files/Autodesk/Revit 2024/RevitAPIUI.dll"

namespace Demo

module RevitContext =
    open Autodesk.Revit.DB
    open Autodesk.Revit.UI

    let tryGetDocument (commandData: ExternalCommandData) : Document option =
        if isNull commandData || isNull commandData.Application then
            None
        else
            let uiDoc = commandData.Application.ActiveUIDocument
            if isNull uiDoc then None else Some uiDoc.Document

    let getDocumentTitle (commandData: ExternalCommandData) =
        match tryGetDocument commandData with
        | Some doc when not (isNull doc) -> doc.Title
        | _ -> "(no document)"
