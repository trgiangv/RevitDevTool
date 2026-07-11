#r "C:/Program Files/Autodesk/Revit 2025/RevitAPI.dll"
#r "C:/Program Files/Autodesk/Revit 2025/RevitAPIUI.dll"


open Autodesk.Revit.Attributes
open Autodesk.Revit.UI
open Autodesk.Revit.UI.Selection
open System
open System.Diagnostics

[<Transaction(TransactionMode.Manual)>]
type TraceGeometryCommand() =
    interface IExternalCommand with
        member _.Execute(commandData, _message, _elements) =
            let uiDoc = commandData.Application.ActiveUIDocument
            let selectRef = uiDoc.Selection.PickObject(ObjectType.Element)
            let element = uiDoc.Document.GetElement(selectRef.ElementId)
            let bbox = element.get_BoundingBox(null)
            let centerTop = (bbox.Min + bbox.Max) / 2.0

            Trace.Write(centerTop)
            Trace.Write(bbox)
            Console.WriteLine(centerTop)
            Console.WriteLine(bbox)
            Result.Succeeded