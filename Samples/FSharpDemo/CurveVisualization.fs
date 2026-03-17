namespace FSharpDemo

open System.Diagnostics
open Autodesk.Revit.Attributes
open Autodesk.Revit.DB
open Autodesk.Revit.UI.Selection
open JetBrains.Annotations
open Nice3point.Revit.Toolkit.External

[<Transaction(TransactionMode.Manual)>]
[<UsedImplicitly>]
type CurveVisualization() =
    inherit ExternalCommand()

    override this.Execute() =
        try
            let curveRef = this.UiDocument.Selection.PickObject(ObjectType.Edge, "Select Curve")
            let curve = this.Document.GetElement(curveRef).GetGeometryObjectFromReference(curveRef) :?> Edge
            Trace.Write(curve)
        with
        | ex -> Trace.TraceError($"Error in CurveVisualization: {ex.Message}")

[<Transaction(TransactionMode.Manual)>]
[<UsedImplicitly>]
type CurvesVisualization() =
    inherit ExternalCommand()

    override this.Execute() =
        try
            let curveRefs = this.UiDocument.Selection.PickObjects(ObjectType.Edge, "Select Curves")
            let curves =
                [ for curveRef in curveRefs do
                    match this.Document.GetElement(curveRef).GetGeometryObjectFromReference(curveRef) with
                    | :? Edge as edge -> yield edge
                    | _ -> () ]
            Trace.Write(curves)
        with
        | ex -> Trace.TraceError($"Error in CurvesVisualization: {ex.Message}")

