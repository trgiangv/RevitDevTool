namespace FSharpDemo
open System.Diagnostics
open System.Linq
open Autodesk.Revit.Attributes
open Autodesk.Revit.UI.Selection
open JetBrains.Annotations
open Nice3point.Revit.Toolkit.External

[<Transaction(TransactionMode.Manual)>]
[<UsedImplicitly>]
type XyzVisualization() =
    inherit ExternalCommand()

    override this.Execute() =
        try
            let xyz = this.UiDocument.Selection.PickObject(ObjectType.PointOnElement)
            Trace.Write(xyz.GlobalPoint)
        with
        | ex -> Trace.TraceError($"Error in XyzVisualization: {ex.Message}")

[<Transaction(TransactionMode.Manual)>]
[<UsedImplicitly>]
type XyzsVisualization() =
    inherit ExternalCommand()

    override this.Execute() =
        try
            let xyzRefs = this.UiDocument.Selection.PickObjects(ObjectType.PointOnElement)
            let xyzs = xyzRefs |> Seq.map (fun x -> x.GlobalPoint) |> Seq.toList
            if xyzs.IsEmpty then
                Trace.TraceWarning("No points selected.")
            else
                Trace.Write(xyzs)
        with
        | ex -> Trace.TraceError($"Error in XyzsVisualization: {ex.Message}")

