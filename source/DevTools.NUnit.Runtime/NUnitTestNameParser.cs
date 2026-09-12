using System.Text;
using static DevTools.NUnit.Runtime.NUnitNameSyntax;

namespace DevTools.NUnit.Runtime;

internal static class NUnitTestNameParser
{
    public static void Split(string fullTestName, out string className, out string methodName)
    {
        SplitParts(fullTestName, out className, out methodName);
        className = StripArgumentLists(className);
        methodName = StripArgumentLists(methodName);
    }

    /// <summary>
    /// C# <c>Ns.Type.Method</c> with argument lists stripped. A uid with no
    /// member dot is not a group key (bare DisplayName). When
    /// <paramref name="methodName"/> is the C# method (<c>TestName</c> /
    /// <c>SetName</c> leaves keep a different last segment), it replaces the
    /// parsed method. <paramref name="typeName"/> is the source type segment
    /// (no namespace, no ctor args).
    /// </summary>
    public static string GroupKey(
        string testId,
        string? methodName = null,
        string? typeName = null,
        string? namespaceName = null)
    {
        testId = testId.Trim();
        if (!string.IsNullOrWhiteSpace(methodName) && !string.IsNullOrWhiteSpace(typeName))
            return Qualify(namespaceName, typeName!) + MemberDot + methodName;

        if (LastAtDepthZero(testId, MemberDot) < 0)
            return Canonicalize(testId);

        Split(testId, out var className, out var parsedMethod);
        if (!string.IsNullOrWhiteSpace(methodName)
            && LastAtDepthZero(className, MemberDot) >= 0)
            return className + MemberDot + methodName;

        return className + MemberDot + parsedMethod;
    }

    /// <summary>
    /// Last-dot split of NUnit <c>ITest.FullName</c>: fixture constructor
    /// arguments stay on <paramref name="className"/> /
    /// <paramref name="typeName"/> (<c>Tests("beta.rvt")</c>). MTP
    /// <c>TestMethodIdentifierProperty.TypeName</c> must not use that string;
    /// strip args (see adapter) so IDEs do not tokenize <c>.</c> inside args.
    /// </summary>
    public static void SplitIde(
        string fullTestName,
        out string className,
        out string namespaceName,
        out string typeName,
        out string methodName)
    {
        SplitParts(fullTestName, out className, out methodName);
        methodName = StripArgumentLists(methodName);
        SplitNamespace(className, out namespaceName, out typeName);
    }

    /// <summary>
    /// CLR metadata type from an NUnit display type: strip ctor args, keep
    /// namespace, normalize generic arity. Used for PDB lookup, not for
    /// parsing <c>ITest.FullName</c>.
    /// </summary>
    public static string ToMetadataTypeName(string displayTypeName)
    {
        SplitNamespace(StripArgumentLists(displayTypeName), out var namespaceName, out var typeName);
        var metadataType = NormalizeGenericSegment(typeName);
        return Qualify(namespaceName, metadataType);
    }

    /// <summary>
    /// ECMA-335 type name without namespace. PDB lookup uses this string.
    /// Fixture ctor args and closed generic args stay off this value.
    /// </summary>
    public static string ToMetadataTypeSegment(string displayTypeName)
    {
        SplitNamespace(StripArgumentLists(displayTypeName), out _, out var typeName);
        return NormalizeGenericSegment(typeName);
    }

    /// <summary>
    /// C# source type name without namespace for MTP
    /// <c>TestMethodIdentifierProperty.TypeName</c>. Visual Studio binds this
    /// to the syntax tree: <c>GenericClosedTests`1</c> and
    /// <c>GenericClosedTests&lt;Int32&gt;</c> both yield "No source available".
    /// Closed generic args stay on uid / <c>ITest.FullName</c>.
    /// </summary>
    public static string ToSourceTypeSegment(string displayTypeName) =>
        StripMetadataArity(ToMetadataTypeSegment(displayTypeName));

    /// <summary>
    /// NUnit keeps closed generic args on the fixture
    /// (<c>GenericClosedTests&lt;Int32&gt;</c>) and constructor args on the
    /// fixture (<c>NamedFixtureSourceTests("alpha.rvt")</c>). Method
    /// <c>ITest.Name</c> is the C# method
    /// (<c>Generic_int_fixture_is_discovered</c>). MTP TypeName cannot carry
    /// constructor args, so two <c>TestFixtureSource</c> instances would share
    /// one leaf label unless those <c>(...)</c> args are copied onto
    /// DisplayName. Do not copy <c>&lt;T&gt;</c> — that is the fixture type,
    /// not a generic method.
    /// </summary>
    public static string AppendDisplayArguments(string displayName, string? displayTypeName)
    {
        if (displayTypeName is not { Length: > 0 })
            return displayName;

        SplitNamespace(displayTypeName, out _, out var typeName);
        var suffixStart = IndexOfArgList(typeName);
        if (suffixStart < 0)
            return displayName;

        var args = typeName[suffixStart..];
        return displayName.EndsWith(args, StringComparison.Ordinal)
            ? displayName
            : displayName + args;
    }

    /// <summary>
    /// TestNode uid must keep the C# method as the last identifier.
    /// <c>TestName</c>/<c>SetName</c> replace NUnit <c>FullName</c> with
    /// <c>Class.Unit_X</c>, which MTP IDEs index as a second method next to
    /// <c>TestMethodIdentifier.MethodName</c>. Map those leaves to
    /// <c>Class.Method("DisplayName")</c>. Ordinary <c>Method(args)</c>
    /// FullNames stay unchanged.
    /// </summary>
    public static string ToIdeTestId(
        string fullName,
        string? className,
        string? methodName,
        string? displayName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return displayName ?? fullName;

        SplitParts(fullName, out var parsedClass, out var rawMethod);
        if (string.IsNullOrWhiteSpace(methodName) || IsGeneratedMethodSegment(rawMethod, methodName!))
            return fullName;

        var type = string.IsNullOrWhiteSpace(className) ? parsedClass : className!;
        var label = string.IsNullOrWhiteSpace(displayName) ? rawMethod : displayName!;
        return string.Concat(type, MemberDot, methodName, ArgOpen, Quote, EscapeDisplay(label), Quote, ArgClose);
    }

    private static bool IsGeneratedMethodSegment(string rawMethod, string methodName) =>
        rawMethod == methodName
        || rawMethod.StartsWith(methodName + ArgOpen, StringComparison.Ordinal)
        || rawMethod.StartsWith(methodName + GenericOpen, StringComparison.Ordinal);

    private static string EscapeDisplay(string value) =>
        value.Replace(Escape.ToString(), @"\\").Replace(Quote.ToString(), "\\\"");

    internal static void SplitParts(string fullTestName, out string className, out string methodName)
    {
        if (TrySplitLast(fullTestName, MemberDot, out className, out methodName))
            return;
        className = fullTestName;
        methodName = fullTestName;
    }

    private static void SplitNamespace(string className, out string namespaceName, out string typeName)
    {
        if (TrySplitLast(className, MemberDot, out namespaceName, out typeName))
            return;
        namespaceName = string.Empty;
        typeName = className;
    }

    private static string NormalizeGenericSegment(string segment)
    {
        if (segment.IndexOf(NestedType) < 0)
            return NormalizeOneGeneric(segment);

        var parts = new List<string>();
        var depth = 0;
        var start = 0;
        for (var index = 0; index < segment.Length; index++)
        {
            var c = segment[index];
            if (c == NestedType && depth == 0)
            {
                parts.Add(NormalizeOneGeneric(segment[start..index]));
                start = index + 1;
            }

            depth = GenericDepth(depth, c);
        }

        parts.Add(NormalizeOneGeneric(segment[start..]));
        return string.Join(NestedType.ToString(), parts);
    }

    private static string NormalizeOneGeneric(string segment)
    {
        var genericStart = segment.IndexOf(GenericOpen);
        if (genericStart < 0)
            return segment;

        var arity = CountClosedGenericArity(segment, genericStart);
        var baseName = segment[..genericStart];
        return arity == 0 ? baseName : baseName + Arity + arity;
    }

    private static int CountClosedGenericArity(string segment, int genericStart)
    {
        var depth = 0;
        var arity = 0;
        for (var index = genericStart; index < segment.Length; index++)
            AddClosedGenericArity(segment[index], ref depth, ref arity);
        return arity;
    }

    private static void AddClosedGenericArity(char c, ref int depth, ref int arity)
    {
        switch (c)
        {
            case GenericOpen:
            {
                depth++;
                if (depth == 1)
                    arity++;
                return;
            }
            case TypeArgComma when depth == 1:
                arity++;
                return;
            case GenericClose:
                depth--;
                break;
        }

    }

    private static string StripMetadataArity(string metadataType)
    {
        if (metadataType.IndexOf(Arity) < 0)
            return metadataType;

        var builder = new StringBuilder(metadataType.Length);
        var index = 0;
        while (index < metadataType.Length)
        {
            if (metadataType[index] == Arity)
            {
                index++;
                while (index < metadataType.Length && char.IsDigit(metadataType[index]))
                    index++;
                continue;
            }

            builder.Append(metadataType[index]);
            index++;
        }

        return builder.ToString();
    }
}
