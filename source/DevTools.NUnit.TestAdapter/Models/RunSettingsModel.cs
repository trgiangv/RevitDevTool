using System.Xml.Serialization;

namespace DevTools.NUnit.TestAdapter.Models;

[XmlRoot("RunSettings")]
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed class RunSettingsModel
{
    [XmlElement("RunConfiguration")]
    public RunConfigurationModel RunConfiguration { get; set; } = new();

    [XmlElement("DevToolsNUnit")]
    public DevToolsNUnitSettingsModel DevToolsNUnit { get; set; } = new();
}

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed class RunConfigurationModel
{
    [XmlElement("CollectSourceInformation")]
    public string? CollectSourceInformation { get; set; }
}

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public sealed class DevToolsNUnitSettingsModel
{
    [XmlElement("HostName")]
    public string? HostName { get; set; }

    [XmlElement("HostVersion")]
    public string? HostVersion { get; set; }

    [XmlElement("HostLaunch")]
    public string? HostLaunch { get; set; }

    [XmlElement("HostTimeout")]
    public string? HostTimeout { get; set; }

    [XmlElement("HostLaunchTimeout")]
    public string? HostLaunchTimeout { get; set; }

    [XmlElement("RunnerPath")]
    public string? RunnerPath { get; set; }
}
