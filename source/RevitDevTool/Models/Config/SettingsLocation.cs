using System.IO ;

namespace RevitDevTool.Models.Config ;

public static class SettingsLocation
{
    public static string GetSettingsPath(string configName)
    {
        var appdata = Environment
            .GetFolderPath( Environment.SpecialFolder.ApplicationData )
            .AppendPath( "RevitDevTool" )
            .AppendPath( Context.Application.VersionNumber );
        
        var settingsDir = appdata.AppendPath("Settings");
        if (!Directory.Exists(settingsDir))
            Directory.CreateDirectory(settingsDir);
        
        return appdata.AppendPath("Settings").AppendPath(configName) ;
    }
}