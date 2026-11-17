namespace DevTools.UI.Behaviors.DragDrop;

public static class DropZones
{
    public const double Band = 0.25;

    public static InsertPosition Position(double offsetY, double height, InsertPosition? previous)
    {
        var ratio = height > 0 ? offsetY / height : 0;
        return previous switch
        {
            InsertPosition.Before => ratio > 0.5 + Band ? InsertPosition.After : InsertPosition.Before,
            InsertPosition.After => ratio < 0.5 - Band ? InsertPosition.Before : InsertPosition.After,
            _ => ratio > 0.5 ? InsertPosition.After : InsertPosition.Before
        };
    }
}
