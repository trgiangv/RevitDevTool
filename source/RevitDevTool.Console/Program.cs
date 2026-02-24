using ConsoleAppFramework;
using RevitDevTool.Console.Commands;

var app = ConsoleApp.Create();
app.Add<Commands>();
await app.RunAsync(args).ConfigureAwait(false);
