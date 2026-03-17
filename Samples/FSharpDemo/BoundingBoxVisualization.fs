namespace FSharpDemo

open System.Diagnostics
open Autodesk.Revit.Attributes
open Autodesk.Revit.DB
open Autodesk.Revit.UI.Selection
open JetBrains.Annotations
open Nice3point.Revit.Toolkit.External

[<Transaction(TransactionMode.Manual)>]
[<UsedImplicitly>]
type BoundingBoxVisualization() =
    inherit ExternalCommand()

    override this.Execute() =
        try
            let elementRef = this.UiDocument.Selection.PickObject(ObjectType.Element, "Select Element")
            let element = this.Document.GetElement(elementRef)
            Trace.Write(elementRef.ElementId)
            Trace.Write(element.get_Parameter(BuiltInParameter.IFC_GUID) |> Option.ofObj |> Option.map (fun p -> p.AsString()) |> Option.defaultValue null)
            Trace.Write(element.UniqueId)
            let bbox = element.get_BoundingBox(this.ActiveView)
            Trace.Write(bbox)
        with
        | :? Autodesk.Revit.Exceptions.OperationCanceledException -> ()
        | ex -> Trace.TraceError($"Error in BoundingBoxVisualization: {ex.Message}")

[<Transaction(TransactionMode.Manual)>]
[<UsedImplicitly>]
type BoundingBoxesVisualization() =
    inherit ExternalCommand()

    override this.Execute() =
        try
            let elementRefs = this.UiDocument.Selection.PickObjects(ObjectType.Element, "Select Elements")
            let boxes =
                [ for elementRef in elementRefs do
                    let element = this.Document.GetElement(elementRef)
                    let bbox = element.get_BoundingBox(this.ActiveView)
                    if bbox <> null then
                        yield bbox
                    else
                        Trace.TraceWarning($"Element {element.Id} has no bounding box.") ]

            if boxes.IsEmpty then
                Trace.TraceWarning("No bounding boxes found.")
            else
                Trace.Write(boxes)
        with
        | ex -> Trace.TraceError($"Error in BoundingBoxesVisualization: {ex.Message}")

[<Transaction(TransactionMode.Manual)>]
[<UsedImplicitly>]
type OutlineVisualization() =
    inherit ExternalCommand()

    override this.Execute() =
        try
            let elementRef = this.UiDocument.Selection.PickObject(ObjectType.Element, "Select Element")
            let element = this.Document.GetElement(elementRef)
            let bbox = element.get_BoundingBox(this.ActiveView)
            let outline = new Outline(bbox.Min, bbox.Max)
            outline.Scale(2.0)
            Trace.Write(outline)
        with
        | :? Autodesk.Revit.Exceptions.OperationCanceledException -> ()
        | ex -> Trace.TraceError($"Error in BoundingBoxVisualization: {ex.Message}")

[<Transaction(TransactionMode.Manual)>]
[<UsedImplicitly>]
type OutlinesVisualization() =
    inherit ExternalCommand()

    override this.Execute() =
        try
            let elementRefs = this.UiDocument.Selection.PickObjects(ObjectType.Element, "Select Elements")
            let boxes =
                [ for elementRef in elementRefs do
                    let element = this.Document.GetElement(elementRef)
                    let bbox = element.get_BoundingBox(this.ActiveView)
                    if bbox <> null then
                        let ol = new Outline(bbox.Min, bbox.Max)
                        ol.Scale(2.0)
                        yield ol
                    else
                        Trace.TraceWarning($"Element {element.Id} has no bounding box.") ]

            if boxes.IsEmpty then
                Trace.TraceWarning("No bounding boxes found.")
            else
                Trace.Write(boxes)
        with
        | ex -> Trace.TraceError($"Error in BoundingBoxesVisualization: {ex.Message}")

