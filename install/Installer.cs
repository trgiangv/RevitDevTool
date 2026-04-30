using Installer;
using WixSharp;
using WixFile = WixSharp.File;

const string outputName = "RevitDevTool";
const string projectName = "RevitDevTool";


var versioning = Versioning.CreateFromVersionStringAsync(args[0]);
var project = new Project
{
    OutDir = "output",
    Name = projectName,
    Platform = Platform.x64,
    UI = WUI.WixUI_FeatureTree,
    MajorUpgrade = MajorUpgrade.Default,
    GUID = new Guid("B2BC2881-A08A-41D8-B1B3-424045E529DB"),
    BannerImage = @"install\Resources\Icons\BannerImage.png",
    BackgroundImage = @"install\Resources\Icons\BackgroundImage.png",
    Version = versioning.VersionPrefix,
    ControlPanelInfo =
    {
        Manufacturer = Environment.UserName,
        ProductIcon = @"install\Resources\Icons\ShellIcon.ico"
    }
};


var bundleFolder = args[1];
var contentsFolder = Path.Combine(bundleFolder, "Contents");
var manifestFile = Path.Combine(bundleFolder, "PackageContents.xml");
var mcpServerExe = Path.Combine(contentsFolder, "MCPServer.exe");
var yearDirs = Directory.GetDirectories(contentsFolder, "*", SearchOption.TopDirectoryOnly);

var wixEntities = Generator.GenerateWixEntities(yearDirs);
var contentsChildren = new WixEntity[] { new WixFile(mcpServerExe) }.Concat(wixEntities).ToArray();

project.Scope = InstallScope.perUser;
project.OutFileName = $"{outputName}-{versioning.Version}";
project.Dirs = [new InstallDir(@"%AppDataFolder%\Autodesk\ApplicationPlugins\RevitDevTool.bundle\",
    new WixFile(manifestFile),
    new Dir("Contents", contentsChildren))];
project.BuildMsi();