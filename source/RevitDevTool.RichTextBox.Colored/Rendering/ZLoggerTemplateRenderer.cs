using RevitDevTool.RichTextBox.Colored.Rtf;
using System.Text.RegularExpressions;

namespace RevitDevTool.RichTextBox.Colored.Rendering;

public class ZLoggerTemplateRenderer : ITokenRenderer
{
    private static readonly Regex TokenPattern = new(@"\{(?<name>\w+)(?::(?<format>[^}]*))?\}", RegexOptions.Compiled);
    private readonly List<ITokenRenderer> _renderers;

    public ZLoggerTemplateRenderer(ZLoggerRichTextBoxOptions options)
    {
        if (string.IsNullOrEmpty(options.OutputTemplate))
        {
            throw new ArgumentNullException(nameof(options.OutputTemplate));
        }

        _renderers = ParseTemplate(options);
    }

    private static List<ITokenRenderer> ParseTemplate(ZLoggerRichTextBoxOptions options)
    {
        var renderers = new List<ITokenRenderer>();
        var template = options.OutputTemplate;
        var theme = options.Theme;
        var lastIndex = 0;

        foreach (Match match in TokenPattern.Matches(template))
        {
            if (match.Index > lastIndex)
            {
                var textBefore = template.Substring(lastIndex, match.Index - lastIndex);
                renderers.Add(new TextTokenRenderer(theme, textBefore));
            }

            var tokenName = match.Groups["name"].Value;
            var format = match.Groups["format"].Success ? match.Groups["format"].Value : string.Empty;

            var renderer = CreateRenderer(tokenName, format, options);
            if (renderer != null)
            {
                renderers.Add(renderer);
            }

            lastIndex = match.Index + match.Length;
        }

        if (lastIndex < template.Length)
        {
            var textAfter = template.Substring(lastIndex);
            renderers.Add(new TextTokenRenderer(theme, textAfter));
        }

        return renderers;
    }

    private static ITokenRenderer? CreateRenderer(string tokenName, string format, ZLoggerRichTextBoxOptions options)
    {
        return tokenName.ToLowerInvariant() switch
        {
            "timestamp" => new TimestampTokenRenderer(options.Theme, format, options.FormatProvider),
            "level" => new LevelTokenRenderer(options.Theme, format),
            "message" => new MessageTokenRenderer(options.Theme),
            "newline" => new NewLineTokenRenderer(),
            "exception" => new ExceptionTokenRenderer(options.Theme),
            "category" => new CategoryTokenRenderer(options.Theme),
            _ => null
        };
    }

    public void Render(ZLoggerLogEntry logEntry, IRtfCanvas canvas)
    {
        foreach (var renderer in _renderers)
        {
            renderer.Render(logEntry, canvas);
        }
    }
}
