#r "C:/Program Files/Autodesk/Revit 2025/RevitAPI.dll"
#r "C:/Program Files/Autodesk/Revit 2025/RevitAPIUI.dll"
#r "nuget: Newtonsoft.Json"
#r "nuget: geometry3Sharp"
#r "nuget: NetTopologySuite"

#load "./modules/Processor.fsx"

open Autodesk.Revit.Attributes
open Autodesk.Revit.UI
open Autodesk.Revit.UI.Selection
open Demo
open Newtonsoft.Json
open g3
open NetTopologySuite.Geometries
open System

[<Transaction(TransactionMode.Manual)>]
type ShowMessageCommand() =
    interface IExternalCommand with
        member _.Execute(commandData, _message, _elements) =
            let text = Processor.getDialogText commandData
            TaskDialog.Show("F# Script", text) |> ignore

            let uiDoc = commandData.Application.ActiveUIDocument
            let selectRef = uiDoc.Selection.PickObject(ObjectType.Element)
            let element = uiDoc.Document.GetElement(selectRef.ElementId)
            let elementType = element.GetType()
            let elementName = element.Name
            let elementCategory = element.Category
            
            Console.WriteLine(elementType)
            Console.WriteLine(elementName)
            Console.WriteLine(elementCategory)

            let elementId = element.Id
            let elementIfcGuid = element.get_Parameter(Autodesk.Revit.DB.BuiltInParameter.IFC_GUID).AsString()
            let elementUniqueId = element.UniqueId
            
            let o = {| X = 2; Y = "Hello" |}

            let sphereGen = Sphere3Generator_NormalizedCube()
            sphereGen.Radius <- 1.0
            sphereGen.Generate() |> ignore
            let meshA = sphereGen.MakeDMesh()

            // box
            let boxGen = GridBox3Generator()
            boxGen.Box <- Box3d(Vector3d(-0.5,-0.5,-0.5), Vector3d(0.5,0.5,0.5))
            boxGen.Generate() |> ignore
            let meshB = boxGen.MakeDMesh()

            // boolean
            let boolean = MeshBoolean()
            boolean.Target <- meshA
            boolean.Tool <- meshB
            boolean.Compute() |> ignore

            let resultMesh = boolean.Result
            Console.WriteLine($"Result triangles: {resultMesh.TriangleCount}")

            let gf = GeometryFactory()

            let polyA =
                gf.CreatePolygon(
                    [|
                        Coordinate(0.0, 0.0)
                        Coordinate(10.0, 0.0)
                        Coordinate(10.0, 10.0)
                        Coordinate(0.0, 10.0)
                        Coordinate(0.0, 0.0)
                    |])

            let polyB =
                gf.CreatePolygon(
                    [|
                        Coordinate(5.0, 0.0)
                        Coordinate(15.0, 0.0)
                        Coordinate(15.0, 10.0)
                        Coordinate(5.0, 10.0)
                        Coordinate(5.0, 0.0)
                    |])

            let union = polyA.Union(polyB)

            Console.WriteLine($"[NTS] Area = {union.Area}")
            Console.WriteLine($"[NTS] Type = {union.GeometryType}")
            printf "[NTS] Points: "

            Console.WriteLine $"%s{JsonConvert.SerializeObject o}"

            Console.WriteLine(elementId)
            Console.WriteLine(elementIfcGuid)
            Console.WriteLine(elementUniqueId)

            Result.Succeeded