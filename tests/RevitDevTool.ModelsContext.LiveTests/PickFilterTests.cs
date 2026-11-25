using Nice3point.Revit.Extensions;
using RevitDevTool.Tools.ModelsContext.Selection;
using System.Text.Json;

namespace RevitDevTool.ModelsContext.LiveTests;

[TestFixture]
public sealed class PickFilterTests : RecordedTest
{
    [Test]
    public void FilterKeepsCheckedOnly()
    {
        Record(new CaseItem
        {
            Id = "PI-03",
            Description = "Pick filter for the Architectural link and Doors: only its doors pass",
            Expected = JsonSerializer.SerializeToElement("door true, wall false, other link door false, link instance true, duct false"),
            ManualSteps = ["Check the Architectural link and Doors, press Pick: only doors of that link highlight; walls, other links and Current model ducts do not."]
        }, () =>
        {
            var model = SampleModel.Host;
            var link = SampleModel.Architectural;
            var instance = (RevitLinkInstance)model.GetElement(link.Key.LinkInstanceId.ToElementId());
            var filter = LinkPickFilter.Create(model, [link.Key], [(long)BuiltInCategory.OST_Doors]);
            Evidence.Step($"Built LinkPickFilter for link instance {instance.Id} and Doors.");

            var expected = new List<string>();
            var actual = new List<string>();
            var door = First(link.Document, BuiltInCategory.OST_Doors) ?? Missing("door", link.Label);
            var wall = First(link.Document, BuiltInCategory.OST_Walls) ?? Missing("wall", link.Label);
            var duct = First(model, BuiltInCategory.OST_DuctCurves) ?? Missing("duct", "the Current model");
            Check(expected, actual, "door", true, filter.AllowReference(LinkReference(door, instance), XYZ.Zero));
            Check(expected, actual, "wall", false, filter.AllowReference(LinkReference(wall, instance), XYZ.Zero));
            if (OtherLinkDoor(model, instance) is { } other)
            {
                Check(expected, actual, "other link door", false, filter.AllowReference(LinkReference(other.Door, other.Instance), XYZ.Zero));
            }
            else
            {
                Evidence.Step("No other loaded link holds a door; the other link check is skipped.");
            }

            Check(expected, actual, "link instance", true, filter.AllowElement(instance));
            Check(expected, actual, "duct", false, filter.AllowElement(duct));
            Evidence.Values(string.Join(", ", expected), string.Join(", ", actual));
            Assert.That(actual, Is.EqualTo(expected));
        });
    }

    private static void Check(List<string> expected, List<string> actual, string name, bool wanted, bool got)
    {
        expected.Add($"{name} {wanted}");
        actual.Add($"{name} {got}");
        Evidence.Step($"{name}: {got}.");
    }

    private static Reference LinkReference(Element element, RevitLinkInstance instance)
    {
        return new Reference(element).CreateLinkReference(instance);
    }

    private static Element? First(Document document, BuiltInCategory category)
    {
        return new FilteredElementCollector(document).OfCategory(category).WhereElementIsNotElementType().FirstElement();
    }

    private static Element Missing(string what, string where)
    {
        Assert.Inconclusive($"No {what} in {where}.");
        return null!;
    }

    private static (RevitLinkInstance Instance, Element Door)? OtherLinkDoor(Document model, RevitLinkInstance picked)
    {
        foreach (var instance in new FilteredElementCollector(model).OfClass(typeof(RevitLinkInstance)).Cast<RevitLinkInstance>())
        {
            if (instance.Id != picked.Id && instance.GetLinkDocument() is { } document && First(document, BuiltInCategory.OST_Doors) is { } door)
            {
                return (instance, door);
            }
        }

        return null;
    }
}
