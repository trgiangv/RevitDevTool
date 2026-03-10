using System.Reflection;

namespace PythonNetStubGenerator;

internal class MethodComparer : IComparer<MethodInfo>
{
    public int Compare(MethodInfo? a, MethodInfo? b)
    {
        if (a == null) return b == null ? 0 : 1;
        if (b == null) return -1;

        var nameCompare = string.Compare(a.NonGenericName(), b.NonGenericName(), StringComparison.InvariantCulture);
        if (nameCompare != 0) return nameCompare;

        var aParams = a.GetParameters();
        var bParams = b.GetParameters();

        var paramCountCompare = aParams.Length.CompareTo(bParams.Length);
        return paramCountCompare != 0 ? paramCountCompare : CompareParameterLists(aParams, bParams);
    }

    private static int CompareParameterLists(ParameterInfo[] aParams, ParameterInfo[] bParams)
    {
        var aParamNames = "";
        var bParamNames = "";

        for (var i = 0; i < aParams.Length; i++)
        {
            aParamNames += aParams[i].Name;
            bParamNames += bParams[i].Name;

            var aType = aParams[i].ParameterType;
            var bType = bParams[i].ParameterType;

            // char and string are equivalent in Python, skip comparison
            if (IsCharStringPair(aType, bType)) continue;

            // Higher depth first → overloads of more specific types come first
            var depthCompare = -GetTypeDepth(aType, true).CompareTo(GetTypeDepth(bType, true));
            if (depthCompare != 0) return depthCompare;
        }

        return string.Compare(aParamNames, bParamNames, StringComparison.Ordinal);
    }

    private static bool IsCharStringPair(Type a, Type b)
        => (a == typeof(char) && b == typeof(string)) || (a == typeof(string) && b == typeof(char));

    /// <summary>
    /// Compute a depth score for a type based on its inheritance and interface hierarchy.
    /// Generic arguments add fractional depth to break ties.
    /// </summary>
    private static float GetTypeDepth(Type? t, bool includeGenerics)
    {
        if (t == null) return 0;

        var baseDepth = t.GetInterfaces()
            .Append(t.BaseType)
            .Select(it => GetTypeDepth(it, false))
            .Max() + 1;

        if (includeGenerics)
            baseDepth += GetGenericDepthBonus(t);

        return baseDepth;
    }

    private static float GetGenericDepthBonus(Type t)
    {
        var generics = new List<Type>();
        if (t.HasElementType) generics.Add(t.GetElementType()!);
        if (t.IsGenericType) generics.AddRange(t.GetGenericArguments());

        return generics.Count > 0
            ? generics.Select(it => GetTypeDepth(it, false)).Max() * .001f
            : 0f;
    }
}
