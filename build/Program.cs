using Build.Cli;
using ConsoleAppFramework;

var app = ConsoleApp.Create();
app.UseFilter<RefusalFilter>();
app.Add<Targets>();
await app.RunAsync(args);
return Environment.ExitCode;
