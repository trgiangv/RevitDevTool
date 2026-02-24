#r "C:/Program Files/Autodesk/Revit 2024/RevitAPI.dll"
#r "C:/Program Files/Autodesk/Revit 2024/RevitAPIUI.dll"
#r "C:/Users/truon/AppData/Roaming/Autodesk/Revit/Addins/2024/RevitDevTool/RevitDevTool.dll"

#load "./modules/Processor.fsx"

open Autodesk.Revit.Attributes
open Autodesk.Revit.UI
open Autodesk.Revit.UI.Selection
open Nice3point.Revit.Toolkit
open System.Diagnostics
open Demo
open System

[<TransactionAttribute(TransactionMode.Manual)>]
type ShowMessageCommand() =
    interface IExternalCommand with
        member _.Execute(commandData, _message, _elements) =
            let text = Processor.getDialogText commandData
            TaskDialog.Show("F# Script", text) |> ignore

            let selectRef = Context.ActiveUiDocument.Selection.PickObject(ObjectType.Element)
            let element = Context.ActiveDocument.GetElement(selectRef.ElementId)
            let elementType = element.GetType()
            let elementName = element.Name
            let elementCategory = element.Category
            
            Console.WriteLine(elementType)
            Console.WriteLine(elementName)
            Console.WriteLine(elementCategory)

            let elementId = element.Id
            let elementIfcGuid = element.get_Parameter(Autodesk.Revit.DB.BuiltInParameter.IFC_GUID).AsString()
            let elementUniqueId = element.UniqueId

            // Console.WriteLine(elementId)
            // Console.WriteLine(elementIfcGuid)
            // Console.WriteLine(elementUniqueId)

            Debug.WriteLine(elementId)
            Debug.WriteLine(elementIfcGuid)
            Debug.WriteLine(elementUniqueId)


            Result.Succeeded
