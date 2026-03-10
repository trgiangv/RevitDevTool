namespace PythonNetStubGenerator;

public sealed class SymbolScope : IDisposable
{
    private readonly List<string> _reservedSymbols;
    public static readonly List<SymbolScope> Scopes = [];
    private readonly string _namespace;

    public SymbolScope(IEnumerable<string> reservedSymbols, string nameSpace)
    {
        _namespace = nameSpace;
        _reservedSymbols = new List<string>(reservedSymbols);
        Scopes.Add(this);
    }

    public void Dispose()
    {
        Scopes.Remove(this);
    }

    public bool HasConflict(string cleanName, string? typeNamespace) => 
        typeNamespace != _namespace && _reservedSymbols.Contains(cleanName);
}