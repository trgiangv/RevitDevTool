namespace PythonNetStubGenerator;

public class ClassScope : IDisposable
{
    private static readonly List<ClassScope> ClassScopes = [];
    public static ClassScope? Current => ClassScopes.LastOrDefault();
    public string PythonClass { get; }
    private Type[] Generics { get; }
    private bool ShouldShadowGenerics { get; }
    public string OutsideAccessor { get; }

    private IndentScope? IndentScope { get; set; }

    public ClassScope(string pythonClass, IEnumerable<Type> newGenerics, bool shouldShadowGenerics)
    {
        Generics = newGenerics.ToArray();
        ShouldShadowGenerics = shouldShadowGenerics;
        PythonClass = pythonClass;
        OutsideAccessor = ScopeAccessor;
        ClassScopes.Add(this);
    }

    public void EnterIndent() => IndentScope ??= new IndentScope();

    public void Dispose()
    {
        var index = ClassScopes.Count - 1;
        var existing = ClassScopes[index];
        ClassScopes.RemoveAt(index);
        IndentScope?.Dispose();
        if (existing != this) throw new Exception();
        GC.SuppressFinalize(this);
    }

    private static string ScopeAccessor =>
        ClassScopes.Count == 0 ? "" :
            string.Join(".", ClassScopes.Select(it => it.PythonClass)) + ".";

    public static IEnumerable<Type> AccessibleGenerics
    {
        get
        {
            var start = ClassScopes.FindLastIndex(it => it.ShouldShadowGenerics);
            if (start == -1) start = 0;
            return ClassScopes.Skip(start).SelectMany(it => it.Generics);
        }
    }
}