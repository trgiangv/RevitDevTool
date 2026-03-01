namespace RevitDevTool.Scintilla.Formatting;

public interface ILogEnrichmentCallbacks
{
    bool ShouldPrettyPrint(in LogRenderContext context);
    bool TryGetStructuredPayload(in LogRenderContext context, out ReadOnlyMemory<byte> utf8Json);
    bool TryResolveToken(in TokenCandidateContext candidate, out TokenResolution resolution);
    void OnTokenResolved(in TokenResolvedContext resolved);
    void OnTokenClick(in TokenClickContext click);
}
