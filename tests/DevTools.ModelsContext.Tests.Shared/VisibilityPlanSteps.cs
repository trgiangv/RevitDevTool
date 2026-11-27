using DevTools.ModelsContext.Visibility;

namespace DevTools.ModelsContext.Tests.Shared;

public static class VisibilityPlanSteps
{
    public static bool UsesLinkFilter(this VisibilityPlan plan)
    {
        return plan.Steps.Any(step => step is LinkFilterStep);
    }
}
