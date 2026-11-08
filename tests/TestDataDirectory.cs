using System;
using System.IO;
using System.Runtime.CompilerServices;

internal static class TestDataDirectory
{
    [ModuleInitializer]
    internal static void Use()
    {
        const string variable = "REVITDEVTOOL_DATA_DIR";
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(variable)))
            Environment.SetEnvironmentVariable(variable, Path.Combine(Path.GetTempPath(), "RevitDevTool-TestData"));
    }
}
