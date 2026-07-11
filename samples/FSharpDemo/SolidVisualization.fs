namespace FSharpDemo

open System.Diagnostics
open Autodesk.Revit.Attributes
open Autodesk.Revit.UI.Selection
open JetBrains.Annotations
open Nice3point.Revit.Toolkit.External
open RevitDevTool.FSharpDemo.Extensions.GeometryExtensions

[<Transaction(TransactionMode.Manual)>]
[<UsedImplicitly>]
type SolidVisualization() =
    inherit ExternalCommand()

    override this.Execute() =
        try
            let solidRef = this.Application.ActiveUIDocument.Selection.PickObject(ObjectType.Element, "Select Solid Element")
            let solids = getSolids (this.Application.ActiveUIDocument.Document.GetElement(solidRef))
            if solids.IsEmpty then
                Trace.TraceWarning("No solid found for the selected element.")
            else
                Trace.Write(solids |> List.head)
        with
        | ex -> Trace.TraceError($"Error in SolidVisualization: {ex.Message}")

[<Transaction(TransactionMode.Manual)>]
[<UsedImplicitly>]
type SolidsVisualization() =
    inherit ExternalCommand()

    override this.Execute() =
        try
            let solidRefs = this.Application.ActiveUIDocument.Selection.PickObjects(ObjectType.Element, "Select Solid Elements")
            let solids =
                solidRefs
                |> Seq.collect (fun sRef -> getSolids (this.Application.ActiveUIDocument.Document.GetElement(sRef)))
                |> Seq.toList
            if solids.IsEmpty then
                Trace.TraceWarning("No solids found for the selected elements.")
            else
                Trace.Write(solids)
        with
        | ex -> Trace.TraceError($"Error in SolidsVisualization: {ex.Message}")

