using DevTools.ModelsContext.Model;
using DevTools.ModelsContext.Tests.Shared;
using DevTools.ModelsContext.ViewModels;

namespace DevTools.ModelsContext.Sandbox;

internal static class FilterExamples
{
    public const long Walls = -2000011;
    public const long Doors = -2000023;
    public const long Ducts = -2008000;

    public static void UpperLevelDoorsWithoutMark(FilterTabViewModel filter, ParameterKey? mark = null)
    {
        Tick(filter, "Doors");
        var root = filter.Root;
        root.Children.Clear();
        root.Match = SetMatch.All;
        var blank = Group(root, SetMatch.Any);
        Condition(blank, mark ?? FakeParameters.Mark, FilterOperator.Undefined);
        Condition(blank, mark ?? FakeParameters.Mark, FilterOperator.Empty);
        var level = Group(root, SetMatch.Any);
        Condition(level, FakeParameters.Level, FilterOperator.Equal, "Level 2");
        Condition(level, FakeParameters.Level, FilterOperator.Equal, "Level 3");
    }

    public static void ArchWallsOrMepDucts(FilterTabViewModel filter)
    {
        Tick(filter, "Ducts", "Walls");
        var root = filter.Root;
        root.Children.Clear();
        root.Match = SetMatch.Any;
        var walls = Group(root, SetMatch.All);
        walls.SetDocumentScope([FakeModelGenerator.ArchSource]);
        walls.SetCategoryScope([Walls]);
        Condition(walls, FakeParameters.FireRating, FilterOperator.Equal, "2h");
        var ducts = Group(root, SetMatch.All);
        ducts.SetDocumentScope([FakeModelGenerator.MepSource]);
        ducts.SetCategoryScope([Ducts]);
        Condition(ducts, FakeParameters.Insulation, FilterOperator.Undefined);
    }

    public static void NestedWideScopes(FilterTabViewModel filter)
    {
        Tick(filter, filter.Categories.Items.Select(category => category.Name).Take(10).ToArray());
        var root = filter.Root;
        root.Children.Clear();
        root.Match = SetMatch.All;
        var parent = root;
        for (var depth = 0; depth < 3; depth++)
        {
            var group = Group(parent, SetMatch.Any);
            foreach (var option in group.DocumentOptions.Concat(group.CategoryOptions).ToList())
            {
                option.IsChecked = true;
            }

            Condition(group, FakeParameters.Mark, FilterOperator.Undefined);
            parent = group;
        }
    }

    public static RuleSetViewModel Group(RuleSetViewModel parent, SetMatch match)
    {
        parent.AddGroupCommand.Execute(null);
        var group = (RuleSetViewModel)parent.Children[^1];
        group.Match = match;
        return group;
    }

    public static RuleViewModel Condition(RuleSetViewModel group, ParameterKey key, FilterOperator op, string value = "")
    {
        group.AddConditionCommand.Execute(null);
        var condition = (RuleViewModel)group.Children[^1];
        condition.SelectedParameter = condition.Parameters.First(parameter => parameter.Key == key);
        condition.Operator = op;
        condition.Value = value;
        return condition;
    }

    private static void Tick(FilterTabViewModel filter, params string[] names)
    {
        filter.Categories.CheckNoneCommand.Execute(null);
        foreach (var category in filter.Categories.Items.Where(category => names.Contains(category.Name)))
        {
            category.IsChecked = true;
        }
    }
}
