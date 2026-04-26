namespace DevTools.McpServer.Tools.Utils;

internal sealed record RevitLaunchContext(
    string Version,
    string RevitPath,
    string LanguageCode,
    IReadOnlyList<string> Arguments);
