namespace Autodesk.Revit.DB;

// Demo-only stub so token callbacks can behave like Revit API types.
public sealed class ElementId
{
    public ElementId(int integerValue)
    {
        IntegerValue = integerValue;
    }

    public int IntegerValue { get; }
    public long Value => IntegerValue;

    public override string ToString()
    {
        return IntegerValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
