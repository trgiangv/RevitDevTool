using RevitDevTool.Bridge.Abstractions;

namespace RevitDevTool.Console.Services.Hosting;

public sealed record RevitHostInstance(string HostVersion, int ProcessId, string PipeName) : IHostInstance
{
    public string AppId => "revit";
}
