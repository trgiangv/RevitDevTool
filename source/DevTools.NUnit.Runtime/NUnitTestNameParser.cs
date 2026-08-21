using System.Text;

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
    /// Last-dot split of NUnit <c>ITest.FullName</c>: fixture constructor
    /// arguments stay on the display type (<c>Tests("beta.rvt")</c>). MTP
    /// <c>TestMethodIdentifierProperty.TypeName</c> must not use this string;
    /// strip args (see adapter) so IDEs do not tokenize <c>.</c> inside args.
    /// </summary>
    public static void SplitIde(string fullTestName, out string namespaceName, out string typeName, out string methodName)
    {
        SplitParts(fullTestName, out var className, out methodName);
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
        return string.IsNullOrEmpty(namespaceName) ? metadataType : namespaceName + "." + metadataType;
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
        var suffixStart = IndexOfConstructorArgs(typeName);
        if (suffixStart < 0)
            return displayName;

        var args = typeName[suffixStart..];
        return displayName.EndsWith(args, StringComparison.Ordinal)
            ? displayName
            : displayName + args;
    }

    private static int IndexOfConstructorArgs(string typeName)
    {
        var depth = 0;
        for (var index = 0; index < typeName.Length; index++)
        {
            switch (typeName[index])
            {
                case '<':
                    depth++;
                    break;
                case '>' when depth > 0:
                    depth--;
                    break;
                case '(' when depth == 0:
                    return index;
            }
        }

        return -1;
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
        if (string.IsNullOrWhiteSpace(methodName))
            return fullName;
        if (IsGeneratedMethodSegment(rawMethod, methodName!))
            return fullName;

        var type = string.IsNullOrWhiteSpace(className) ? parsedClass : className!;
        var label = string.IsNullOrWhiteSpace(displayName) ? rawMethod : displayName!;
        return type + "." + methodName + "(\"" + EscapeDisplay(label) + "\")";
    }

    private static bool IsGeneratedMethodSegment(string rawMethod, string methodName) =>
        rawMethod == methodName
        || rawMethod.StartsWith(methodName + "(", StringComparison.Ordinal)
        || rawMethod.StartsWith(methodName + "<", StringComparison.Ordinal);

    private static string EscapeDisplay(string value) =>
        value.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static void SplitParts(string fullTestName, out string className, out string methodName)
    {
        var lastDot = LastSeparatorAtDepthZero(fullTestName, '.');
        if (lastDot < 0)
        {
            className = fullTestName;
            methodName = fullTestName;
            return;
        }

        className = fullTestName[..lastDot];
        methodName = fullTestName[(lastDot + 1)..];
    }

    private static void SplitNamespace(string className, out string namespaceName, out string typeName)
    {
        var lastDot = LastSeparatorAtDepthZero(className, '.');
        if (lastDot < 0)
        {
            namespaceName = string.Empty;
            typeName = className;
            return;
        }

        namespaceName = className[..lastDot];
        typeName = className[(lastDot + 1)..];
    }

    private static int LastSeparatorAtDepthZero(string value, char separator)
    {
        var depth = 0;
        var last = -1;
        for (var index = 0; index < value.Length; index++)
        {
            switch (value[index])
            {
                case '(':
                case '<':
                    depth++;
                    break;
                case ')':
                case '>':
                    if (depth > 0)
                        depth--;
                    break;
                default:
                    if (value[index] == separator && depth == 0)
                        last = index;
                    break;
            }
        }

        return last;
    }

    private static string StripArgumentLists(string value)
    {
        var depth = 0;
        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
        {
            if (c == '(')
            {
                depth++;
                continue;
            }

            if (c == ')' && depth > 0)
            {
                depth--;
                continue;
            }

            if (depth == 0)
                builder.Append(c);
        }

        return builder.ToString();
    }

    private static string NormalizeGenericSegment(string segment)
    {
        if (segment.IndexOf('+') < 0)
            return NormalizeOneGeneric(segment);

        var parts = new List<string>();
        var depth = 0;
        var start = 0;
        for (var index = 0; index < segment.Length; index++)
        {
            switch (segment[index])
            {
                case '<' or '(':
                    depth++;
                    break;
                case '>' or ')' when depth > 0:
                    depth--;
                    break;
                case '+' when depth == 0:
                    parts.Add(NormalizeOneGeneric(segment[start..index]));
                    start = index + 1;
                    break;
            }
        }

        parts.Add(NormalizeOneGeneric(segment[start..]));
        return string.Join("+", parts);
    }

    private static string NormalizeOneGeneric(string segment)
    {
        var genericStart = segment.IndexOf('<');
        if (genericStart < 0)
            return segment;

        var baseName = segment[..genericStart];
        var depth = 0;
        var typeArgumentCount = 0;
        for (var index = genericStart; index < segment.Length; index++)
        {
            switch (segment[index])
            {
                case '<':
                    depth++;
                    if (depth == 1)
                        typeArgumentCount++;
                    break;
                case ',' when depth == 1:
                    typeArgumentCount++;
                    break;
                case '>':
                    depth--;
                    break;
            }
        }

        return typeArgumentCount == 0 ? baseName : baseName + "`" + typeArgumentCount;
    }

    private static string StripMetadataArity(string metadataType)
    {
        if (metadataType.IndexOf('`') < 0)
            return metadataType;

        var builder = new StringBuilder(metadataType.Length);
        for (var index = 0; index < metadataType.Length; index++)
        {
            if (metadataType[index] == '`')
            {
                index++;
                while (index < metadataType.Length && char.IsDigit(metadataType[index]))
                    index++;
                index--;
                continue;
            }

            builder.Append(metadataType[index]);
        }

        return builder.ToString();
    }
}
