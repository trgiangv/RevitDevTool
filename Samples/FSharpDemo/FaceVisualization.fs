namespace FSharpDemo

open System.Diagnostics
open Autodesk.Revit.Attributes
open Autodesk.Revit.DB
open Autodesk.Revit.UI.Selection
open JetBrains.Annotations
open Nice3point.Revit.Toolkit.External

[<Transaction(TransactionMode.Manual)>]
[<UsedImplicitly>]
type FaceVisualization() =
    inherit ExternalCommand()

    override this.Execute() =
        try
            let faceRef = this.UiDocument.Selection.PickObject(ObjectType.Face, "Select Face")
            let face = this.Document.GetElement(faceRef).GetGeometryObjectFromReference(faceRef) :?> Face
            Trace.Write(face)
        with
        | ex -> Trace.TraceError($"Error in FaceVisualization: {ex.Message}")

[<Transaction(TransactionMode.Manual)>]
[<UsedImplicitly>]
type FacesVisualization() =
    inherit ExternalCommand()

    override this.Execute() =
        try
            let faceRefs = this.UiDocument.Selection.PickObjects(ObjectType.Face, "Select Faces")
            let faces =
                [ for faceRef in faceRefs do
                    match this.Document.GetElement(faceRef).GetGeometryObjectFromReference(faceRef) with
                    | :? Face as face -> yield face
                    | _ -> Trace.TraceWarning($"Face not found for reference: {faceRef}") ]
            Trace.Write(faces)
        with
        | ex -> Trace.TraceError($"Error in FacesVisualization: {ex.Message}")

