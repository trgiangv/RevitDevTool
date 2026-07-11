module RevitDevTool.FSharpDemo.Extensions.GeometryExtensions

open System.Linq
open Autodesk.Revit.DB

[<AutoOpen>]
module GeometryExtensions =

    let private geometryOptions () =
        new Options(ComputeReferences = false, IncludeNonVisibleObjects = true)

    let getSolids (element: Element) =
        let opts = geometryOptions ()
        let geometry = element.get_Geometry(opts)
        if geometry = null then
            []
        else
            [ for geoObj in geometry do
                match geoObj with
                | :? Solid as solid when solid.Volume <> 0.0 -> yield solid
                | :? GeometryInstance as geoInstance ->
                    for instGeoObj in geoInstance.GetInstanceGeometry() do
                        match instGeoObj with
                        | :? Solid as instSolid when instSolid.Volume <> 0.0 -> yield instSolid
                        | _ -> ()
                | _ -> () ]

    let getMeshes (element: Element) =
        let opts = geometryOptions ()
        let geometry = element.get_Geometry(opts)
        if geometry = null then
            []
        else
            [ for geoObj in geometry do
                match geoObj with
                | :? Mesh as mesh -> yield mesh
                | :? GeometryInstance as geoInstance ->
                    for instGeoObj in geoInstance.GetInstanceGeometry() do
                        match instGeoObj with
                        | :? Mesh as instMesh -> yield instMesh
                        | _ -> ()
                | _ -> () ]

    let getFacesOfSolid (solid: Solid) =
        solid.Faces.Cast<Face>() |> Seq.toList

    let getVerticesOfMesh (mesh: Mesh) =
        mesh.Vertices |> Seq.toList

    let getVerticesOfSolid (solid: Solid) =
        solid.Edges.Cast<Edge>()
        |> Seq.collect (fun edge -> edge.Tessellate())
        |> Seq.toList

    let getVerticesOfGeometryObject (geoObj: GeometryObject) =
        match geoObj with
        | :? Solid as solid -> getVerticesOfSolid solid
        | :? Mesh as mesh -> getVerticesOfMesh mesh
        | _ -> []

    let getEdgesOfSolid (solid: Solid) =
        solid.Edges.Cast<Edge>() |> Seq.toList

    let getCurvesOfSolid (solid: Solid) =
        solid.Edges.Cast<Edge>()
        |> Seq.map (fun edge -> edge.AsCurve())
        |> Seq.toList

    let getEdgesOfFace (face: Face) =
        face.EdgeLoops.Cast<EdgeArray>()
        |> Seq.collect (fun edgeLoop -> edgeLoop.Cast<Edge>())
        |> Seq.toList

    let getCurvesOfFace (face: Face) =
        let curveLoops = face.GetEdgesAsCurveLoops()
        if curveLoops = null then
            []
        else
            curveLoops |> Seq.collect id |> Seq.toList

