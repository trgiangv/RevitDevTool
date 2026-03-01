using Microsoft.Extensions.Logging;
using RevitDevTool.Scintilla.Core;
namespace RevitDevTool.Scintilla.Render;

public interface ILogStyleRegistry
{
    int GetStyleId(LogLevel level);
    int GetStyleId(LogSemanticStyle semanticStyle);
    bool TryGetStyleId(string styleKey, out int styleId);
    IReadOnlyDictionary<string, int> CustomStyleIds { get; }
    int GetTokenIndicatorStyleId();
    int GetLinkHotspotStyleId();
    int TokenIndicatorStyleId { get; }
    Color TokenIndicatorColor { get; }
    bool TokenIndicatorUnderText { get; }
}
