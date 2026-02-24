namespace RevitDevTool.Bridge.Enums.Revit;

/// <summary>
/// Maps to Autodesk.Revit.DB.DetachFromCentralOption.
/// </summary>
public enum CentralMode
{
    DoNotDetach,
    DetachAndPreserveWorksets,
    DetachAndDiscardWorksets,
    ClearTransmittedSaveAsNewCentral
}
