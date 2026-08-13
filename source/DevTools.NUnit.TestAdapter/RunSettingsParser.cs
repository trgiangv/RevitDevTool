using System.Xml.Serialization;
using DevTools.NUnit.TestAdapter.Models;

namespace DevTools.NUnit.TestAdapter;

internal static class RunSettingsParser
{
    public static ParsedRunSettings Parse(string? settingsXml)
    {
        if (string.IsNullOrWhiteSpace(settingsXml))
            return ParsedRunSettings.Empty;

        try
        {
            using var reader = new StringReader(settingsXml);
            var serializer = new XmlSerializer(typeof(RunSettingsModel));
            if (serializer.Deserialize(reader) is RunSettingsModel model)
                return new ParsedRunSettings(
                    model.DevToolsNUnit,
                    model.RunConfiguration);
        }
        catch
        {
            // Fall back to environment/default values when runsettings XML is invalid.
        }

        return ParsedRunSettings.Empty;
    }
}

internal sealed record ParsedRunSettings(DevToolsNUnitSettingsModel DevToolsNUnit, RunConfigurationModel RunConfiguration)
{
    public static ParsedRunSettings Empty { get; } = new(new DevToolsNUnitSettingsModel(), new RunConfigurationModel());

    public bool IsDevToolsNUnitEnabled =>
        !string.IsNullOrWhiteSpace(DevToolsNUnit.HostVersion);
}
