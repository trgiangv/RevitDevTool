namespace FSharpDemo

open System.Diagnostics
open Autodesk.Revit.Attributes
open Autodesk.Revit.UI.Selection
open JetBrains.Annotations
open Nice3point.Revit.Toolkit.External
open RevitDevTool.FSharpDemo.Extensions.GeometryExtensions

[<Transaction(TransactionMode.Manual)>]
[<UsedImplicitly>]
type MeshVisualization() =
    inherit ExternalCommand()

    override this.Execute() =
        try
            let meshRef = this.UiDocument.Selection.PickObject(ObjectType.Element, "Select Mesh Element")
            let meshes = getMeshes (this.Document.GetElement(meshRef))
            Trace.Write(meshes |> List.head)
        with
        | ex -> Trace.TraceError($"Error in MeshVisualization: {ex.Message}")

[<Transaction(TransactionMode.Manual)>]
[<UsedImplicitly>]
type MeshesVisualization() =
    inherit ExternalCommand()

    override this.Execute() =
        try
            let meshRefs = this.UiDocument.Selection.PickObjects(ObjectType.Element, "Select Mesh Elements")
            let meshes =
                meshRefs
                |> Seq.collect (fun mRef -> getMeshes (this.Document.GetElement(mRef)))
                |> Seq.toList
            Trace.Write(meshes)
        with
        | ex -> Trace.TraceError($"Error in MeshesVisualization: {ex.Message}")

