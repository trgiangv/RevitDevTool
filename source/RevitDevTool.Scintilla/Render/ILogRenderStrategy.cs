using Microsoft.Extensions.Logging;
namespace RevitDevTool.Scintilla.Render;

public interface ILogRenderStrategy
{
    int GetStyleId(LogLevel level);
    void ConfigureStyles(IStyleWriter styleWriter);
}
