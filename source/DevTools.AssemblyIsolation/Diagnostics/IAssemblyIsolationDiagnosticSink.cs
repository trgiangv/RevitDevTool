namespace DevTools.AssemblyIsolation.Diagnostics;

public interface IAssemblyIsolationDiagnosticSink
{
    void Publish(AssemblyIsolationDiagnostic diagnostic);
}
