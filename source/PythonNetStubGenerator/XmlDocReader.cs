using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
// ReSharper disable RedundantSuppressNullableWarningExpression

namespace PythonNetStubGenerator;

/// <summary>
/// Reads and parses a single .NET XML documentation file.
/// Extracts summary, param, returns, remarks, exception, example, value tags.
/// Handles inner XML tags like see, paramref, typeparamref, c.
/// </summary>
public sealed class XmlDocReader
{
    private static readonly Regex WhitespaceCollapse = new(@"[ \t]*\r?\n[ \t]*", RegexOptions.Compiled);
    private static readonly Regex MultiSpace = new("  +", RegexOptions.Compiled);

    private readonly Dictionary<string, XElement> _members = new(StringComparer.Ordinal);

    private XmlDocReader() { }

    /// <summary>
    /// Try to create a reader from the XML file adjacent to the assembly DLL.
    /// Returns null if no XML file is found or parsing fails.
    /// </summary>
    public static XmlDocReader? TryCreateFromAssembly(Assembly assembly)
    {
        if (assembly.IsDynamic || string.IsNullOrEmpty(assembly.Location))
            return null;

        var xmlPath = FindXmlDocPath(assembly.Location);
        return xmlPath != null ? TryCreateFromFile(xmlPath) : null;
    }

    /// <summary>
    /// Try to create a reader from a specific XML documentation file path.
    /// </summary>
    private static XmlDocReader? TryCreateFromFile(string xmlPath)
    {
        try
        {
            var doc = XDocument.Load(xmlPath);
            var reader = new XmlDocReader();

            foreach (var member in doc.Root?.Element("members")?.Elements("member") ?? [])
            {
                var name = member.Attribute("name")?.Value;
                if (!string.IsNullOrEmpty(name))
                    reader._members[name!] = member;
            }

            return reader._members.Count > 0 ? reader : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Number of documented members in this reader.</summary>
    public int Count => _members.Count;

    /// <summary>
    /// Get documentation for any MemberInfo.
    /// Falls back to matching without parameter list for overloaded methods.
    /// </summary>
    public DocComment? GetDoc(MemberInfo member)
    {
        try
        {
            var id = XmlMemberIdBuilder.GetMemberId(member);

            if (_members.TryGetValue(id, out var element))
                return ParseElement(element);

            // Fallback: for methods with overloads, try without parameter list
            if (member is not MethodBase mb || mb.GetParameters().Length <= 0) return null;
            var parenIndex = id.IndexOf('(');
            if (parenIndex > 0 && _members.TryGetValue(id[..parenIndex], out element))
                return ParseElement(element);

            return null;
        }
        catch
        {
            return null;
        }
    }

    #region Parsing

    private static DocComment ParseElement(XElement element)
    {
        var parameters = new Dictionary<string, string>();
        foreach (var param in element.Elements("param"))
        {
            var name = param.Attribute("name")?.Value;
            if (!string.IsNullOrEmpty(name))
                parameters[name!] = RenderContent(param).Trim();
        }

        var exceptions = new List<ExceptionDoc>();
        foreach (var exc in element.Elements("exception"))
        {
            var typeName = ExtractShortName(exc.Attribute("cref")?.Value);
            if (!string.IsNullOrEmpty(typeName))
                exceptions.Add(new ExceptionDoc
                {
                    TypeName = typeName,
                    Description = RenderContent(exc).Trim()
                });
        }

        return new DocComment
        {
            Summary = GetElementText(element, "summary"),
            Returns = GetElementText(element, "returns"),
            Remarks = GetElementText(element, "remarks"),
            Example = GetElementText(element, "example"),
            Value = GetElementText(element, "value"),
            Parameters = parameters,
            Exceptions = exceptions
        };
    }

    private static string? GetElementText(XElement parent, string elementName)
    {
        var el = parent.Element(elementName);
        if (el == null) return null;
        var text = NormalizeWhitespace(RenderContent(el).Trim());
        return string.IsNullOrEmpty(text) ? null : text;
    }

    #endregion

    #region Content Rendering

    /// <summary>
    /// Render the inner content of an XML element, converting tags to readable text.
    /// </summary>
    private static string RenderContent(XElement element)
    {
        var sb = new StringBuilder();
        RenderContentTo(sb, element);
        return sb.ToString();
    }

    private static void RenderContentTo(StringBuilder sb, XElement element)
    {
        foreach (var node in element.Nodes())
        {
            switch (node)
            {
                case XText text:
                    sb.Append(text.Value);
                    break;

                case XElement child:
                    RenderXmlElement(sb, child);
                    break;
            }
        }
    }

    private static void RenderXmlElement(StringBuilder sb, XElement child)
    {
        switch (child.Name.LocalName)
        {
            case "see" or "seealso":
                RenderSeeElement(sb, child);
                break;

            case "paramref" or "typeparamref":
                RenderParamRefElement(sb, child);
                break;

            case "c" or "code":
                RenderCodeElement(sb, child);
                break;

            case "para":
                RenderParagraphElement(sb, child);
                break;

            case "list":
                RenderListElement(sb, child);
                break;

            default:
                RenderContentTo(sb, child);
                break;
        }
    }

    private static void RenderSeeElement(StringBuilder sb, XElement child)
    {
        var cref = child.Attribute("cref")?.Value;
        sb.Append(cref != null
            ? ExtractShortNameNoBacktick(cref)
            : child.Attribute("langword")?.Value ?? child.Value);
    }

    private static void RenderParamRefElement(StringBuilder sb, XElement child)
    {
        sb.Append(child.Attribute("name")?.Value ?? "");
    }

    private static void RenderCodeElement(StringBuilder sb, XElement child)
    {
        sb.Append(child.Value);
    }

    private static void RenderParagraphElement(StringBuilder sb, XElement child)
    {
        sb.AppendLine();
        RenderContentTo(sb, child);
    }

    private static void RenderListElement(StringBuilder sb, XElement child)
    {
        foreach (var item in child.Elements("item"))
            RenderListItem(sb, item);
    }

    private static void RenderListItem(StringBuilder sb, XElement item)
    {
        sb.AppendLine();

        var term = item.Element("term");
        sb.Append(term != null ? $"  - {RenderContent(term).Trim()}: " : "  - ");

        var desc = item.Element("description");
        if (desc != null)
            sb.Append(RenderContent(desc).Trim());
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Locate the XML documentation file for an assembly.
    /// Checks: same directory, then "en/" subdirectory.
    /// </summary>
    private static string? FindXmlDocPath(string assemblyLocation)
    {
        var xmlPath = Path.ChangeExtension(assemblyLocation, ".xml");
        if (File.Exists(xmlPath)) return xmlPath;

        var dir = Path.GetDirectoryName(assemblyLocation);
        if (dir == null) return null;

        var altPath = Path.Combine(dir, "en", Path.GetFileNameWithoutExtension(assemblyLocation) + ".xml");
        return File.Exists(altPath) ? altPath : null;
    }

    /// <summary>
    /// Extract short type name from a cref value.
    /// "T:System.ArgumentNullException" → "ArgumentNullException"
    /// </summary>
    private static string ExtractShortName(string? cref)
    {
        if (string.IsNullOrEmpty(cref)) return "";
        var name = cref.Contains(':') ? cref![(cref.IndexOf(':') + 1)..] : cref;
        var lastDot = name!.LastIndexOf('.');
        return lastDot >= 0 ? name[(lastDot + 1)..] : name;
    }

    /// <summary>
    /// Extract short name and strip generic backtick notation.
    /// "T:System.Collections.Generic.List`1" → "List"
    /// </summary>
    private static string ExtractShortNameNoBacktick(string cref)
    {
        var name = ExtractShortName(cref);
        var backtick = name.IndexOf('`');
        return backtick >= 0 ? name[..backtick] : name;
    }

    private static string NormalizeWhitespace(string text)
    {
        text = WhitespaceCollapse.Replace(text, " ");
        text = MultiSpace.Replace(text, " ");
        return text.Trim();
    }

    #endregion
}
