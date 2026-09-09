# C# and F# scripting

Use standard Roslyn scripting files and NuGet package directives:

```csharp
#r "nuget: Humanizer"

Console.WriteLine("Hello from a host-aware C# script");
```

The runtime supplies Autodesk references, resolves package dependencies, and executes the script through the host-safe execution path.
