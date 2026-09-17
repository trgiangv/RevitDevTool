#r "C:/Program Files/Autodesk/Revit 2025/RevitAPI.dll"
#r "C:/Program Files/Autodesk/Revit 2025/RevitAPIUI.dll"

open System
open System.Diagnostics
open Autodesk.Revit.Attributes
open Autodesk.Revit.DB
open Autodesk.Revit.UI
open Autodesk.Revit.UI.Selection

[<Transaction(TransactionMode.Manual)>]
type LinkElementTraceCommand() =
    interface IExternalCommand with
        member _.Execute(commandData, _message, _elements) =
            let uidoc = commandData.Application.ActiveUIDocument
            if isNull uidoc || isNull uidoc.Document then
                Trace.WriteLine("No active document. Open a model, then run again.")
                Result.Cancelled
            else
                try
                    let picks =
                        uidoc.Selection.PickObjects(ObjectType.LinkedElement, "Select linked element(s)")
                    let hostDoc = uidoc.Document
                    for pick in picks do
                        match hostDoc.GetElement(pick.ElementId) with
                        | :? RevitLinkInstance as instance ->
                            let linked =
                                match instance.GetLinkDocument() with
                                | null -> null
                                | linkDoc -> linkDoc.GetElement(pick.LinkedElementId)
                            if isNull linked then
                                Trace.WriteLine($"Unloaded or missing link instance {instance.Id}")
                            else
                                // Scoped tokens: {linkInstanceId}@{inner}. Click in the monitor to select + zoom.
                                Trace.WriteLine($"Linked ElementId {instance.Id}@{linked.Id}")
                                Trace.WriteLine($"Linked UniqueId {instance.Id}@{linked.UniqueId}")
                                let ifcParam = linked.get_Parameter(BuiltInParameter.IFC_GUID)
                                if not (isNull ifcParam) then
                                    let ifc = ifcParam.AsString()
                                    if not (String.IsNullOrEmpty(ifc)) then
                                        Trace.WriteLine($"Linked IfcGuid {instance.Id}@{ifc}")
                        | _ -> ()
                    Result.Succeeded
                with
                | :? Autodesk.Revit.Exceptions.OperationCanceledException -> Result.Cancelled
