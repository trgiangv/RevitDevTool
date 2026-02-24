#load "../core/Utils.fsx"
#load "./DocumentSummary.fsx"

namespace Demo

module Processor =
    open Autodesk.Revit.UI

    let getDialogText (commandData: ExternalCommandData) =
        DocumentSummary.buildSummary commandData |> Demo.Utils.joinLines