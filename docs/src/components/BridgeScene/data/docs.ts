import type {DocPage} from '../types';

const page = (shortLabel: string, title: string, path: string): DocPage => ({shortLabel, title, path});

export const DOC_PAGES = {
  pythonScripts: page('Python scripts', 'Python scripts', 'execution/python/Execution-Python'),
  pythonDependencies: page('Python deps', 'Python dependencies', 'execution/Python-Dependencies'),
  pythonEcosystems: page('Ecosystems', 'Python ecosystems', 'execution/python/Execution-PythonEcosystems'),
  debugger: page('Debugging', 'Attach a debugger', 'execution/python/Execution-PythonDebugging'),
  stubs: page('Stubs', 'Stub generation', 'execution/python/Execution-StubGeneration'),
  ironPythonFolders: page('pyRevit folders', 'IronPython and pyRevit folders', 'execution/ironpython/Execution-VsPyRevit'),
  ironPythonStandalone: page('Standalone', 'Standalone IronPython', 'execution/ironpython/Execution-IronPythonStandalone'),
  csharpScripts: page('C# scripts', 'C# scripts', 'execution/csharp-script/Execution-CSharp'),
  fsharpScripts: page('F# scripts', 'F# scripts', 'execution/fsharp-script/Execution-FSharp'),
  assembly: page('.NET assembly', '.NET assembly', 'execution/dotnet-assembly/Execution-Assembly'),
  nuget: page('NuGet', 'NuGet dependencies', 'execution/NuGet-Dependencies'),
  executionOverview: page('Overview', 'Execution overview', 'execution/Execution-Overview'),
  pytest: page('pytest', 'pytest', 'testing/pytest'),
  unittest: page('unittest', 'unittest for IronPython', 'testing/unittest'),
  nunit: page('NUnit', 'NUnit tests', 'testing/NUnit'),
  testingOverview: page('Testing', 'Testing overview', 'testing/Testing-Overview'),
  troubleshooting: page('Troubleshoot', 'Troubleshooting', 'getting-started/Troubleshooting'),
  mcpProtocol: page('MCP protocol', 'MCP protocol', 'mcp/MCP-Protocol'),
  mcpCSharp: page('MCP .NET', 'MCP .NET toolsets', 'mcp/MCP-CSharp'),
  mcpPython: page('MCP Python', 'MCP Python toolsets', 'mcp/MCP-Python'),
  mcpToolsets: page('MCP toolsets', 'MCP toolsets example', 'examples/Examples-MCPToolsets'),
} as const;

export function docPath(doc: DocPage): string {
  return `/docs/${doc.path}`;
}
