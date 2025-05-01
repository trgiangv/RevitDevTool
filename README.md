# RevitDevTool

Autodesk Revit plugin project organized into multiple solution files that target versions 2021 - 2026.

## Table of content

<!-- TOC -->
* [Usage](#usage)
  * [Trace Log](#trace-log)
  * [Trace Geometry](#trace-geometry)
* [Prerequisites](#prerequisites)
* [Solution Structure](#solution-structure)
* [Project Structure](#project-structure)
* [Building](#building)
  * [Building the MSI installer and the Autodesk bundle on local machine](#building-the-msi-installer-and-the-autodesk-bundle-on-local-machine)
* [Publishing Releases](#publishing-releases)
  * [Creating a new Release from the JetBrains Rider](#creating-a-new-release-from-the-jetbrains-rider)
  * [Creating a new Release from the Terminal](#creating-a-new-release-from-the-terminal)
  * [Creating a new Release on GitHub](#creating-a-new-release-on-github)
* [Compiling a solution on GitHub](#compiling-a-solution-on-github)
* [Conditional compilation for a specific Revit version](#conditional-compilation-for-a-specific-revit-version)
* [Managing Supported Revit Versions](#managing-supported-revit-versions)
  * [Solution configurations](#solution-configurations)
  * [Project configurations](#project-configurations)
* [API references](#api-references)
* [Learn More](#learn-more)
<!-- TOC -->

## Usage

### Trace Log

Open `Trace Log` DockablePanel and `Enable` the logger. Then you can then use `Trace.TraceInformation()`, `Trace.TraceWarning()`, or `Trace.TraceError()` in your code to record debugging information and monitor the health of your application running.

### Trace Geometry

Turn on the `TraceGeometry` switch in the `RevitDevTools` panel, and use `Trace.Write(GeometryObject)` or `Trace.Write(IEnumerable<GeometryObject>)` to generate transient geometry.

`Transient Geometry` is a read-only temporary primitive that works like Dynamo's preview. `Transient Geometry` cannot be selected, so it cannot be deleted directly. It will be destroyed after the document is closed and will not be saved to the document. Alternatively, you can use `ClearTraceGeometry` to delete the it created by `TraceGeometry`.

## Screenshots

![TraceLog.png](./images/tracelog.png)

## Prerequisites

Before you can build this project, you need to install .NET and IDE.
If you haven't already installed these, you can do so by visiting the following:

- [.NET Framework 4.8](https://dotnet.microsoft.com/download/dotnet-framework/net48)
- [.NET 9](https://dotnet.microsoft.com/en-us/download/dotnet)
- [JetBrains Rider](https://www.jetbrains.com/rider/) or [Visual Studio](https://visualstudio.microsoft.com/)

After installation, clone this repository to your local machine and navigate to the project directory.

## Solution Structure

| Folder  | Description                                                                |
|---------|----------------------------------------------------------------------------|
| build   | Nuke build system. Used to automate project builds                         |
| install | Add-in installer, called implicitly by the Nuke build                      |
| source  | Project source code folder. Contains all solution projects                 |
| output  | Folder of generated files by the build system, such as bundles, installers |

## Project Structure

| Folder     | Description                                                                                                                                                                                          |
|------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Commands   | External commands invoked from the Revit ribbon. Registered in the `Application` class                                                                                                               |
| Models     | Classes that encapsulate the app's data, include data transfer objects (DTOs). More [details](https://learn.microsoft.com/en-us/dotnet/architecture/maui/mvvm).                                      |
| ViewModels | Classes that implement properties and commands to which the view can bind data. More [details](https://learn.microsoft.com/en-us/dotnet/architecture/maui/mvvm).                                     |
| Views      | Classes that are responsible for defining the structure, layout and appearance of what the user sees on the screen. More [details](https://learn.microsoft.com/en-us/dotnet/architecture/maui/mvvm). |
| Resources  | Images, sounds, localisation files, etc.                                                                                                                                                             |
| Utils      | Utilities, extensions, helpers used across the application                                                                                                                                           |

## Building

We recommend JetBrains Rider as preferred IDE, since it has outstanding .NET support. If you don't have Rider installed, you can download it
from [here](https://www.jetbrains.com/rider/).

1. Open JetBrains Rider
2. In the `Solutions Configuration` drop-down menu, select `Release R25` or `Debug R25`. Suffix `R25` means compiling for the Revit 2025.
3. After the solution loads, you can build it by clicking on `Build -> Build Solution`.
4. `Debug` button will start Revit add-in in the debug mode.

   ![image](https://github.com/user-attachments/assets/d209d863-a6d5-43a9-83e1-5eeb2b9fddac)

Also, you can use Visual Studio. If you don't have Visual Studio installed, download it from [here](https://visualstudio.microsoft.com/downloads/).

1. Open Visual Studio
2. In the `Solutions Configuration` drop-down menu, select `Release R25` or `Debug R25`. Suffix `R25` means compiling for the Revit 2025.
3. After the solution loads, you can build it by clicking on `Build -> Build Solution`.

### Building the MSI installer and the Autodesk bundle on local machine

To build the project for all versions, create the installer and bundle, this project uses [NUKE](https://github.com/nuke-build/nuke)

To execute your NUKE build locally, you can follow these steps:

1. **Install NUKE as a global tool**. First, make sure you have NUKE installed as a global tool. You can install it using dotnet CLI:

    ```shell
    dotnet tool install Nuke.GlobalTool --global
    ```

   You only need to do this once on your machine.

2. **Navigate to your project directory**. Open a terminal / command prompt and navigate to your project's root directory.
3. **Run the build**. Once you have navigated to your project's root directory, you can run the NUKE build by calling:

   Compile:
   ```shell
   nuke
   ```

   Create installer:
   ```shell
   nuke createinstaller
   ```

   Create installer and bundle:
   ```shell
   nuke createinstaller createbundle
   ```

   This command will execute the NUKE build defined in your project.

## Publishing Releases

Releases are managed by creating new [Git tags](https://git-scm.com/book/en/v2/Git-Basics-Tagging).
A tag in Git used to capture a snapshot of the project at a particular point in time, with the ability to roll back to a previous version.

Tags must follow the format `version` or `version-stage.n.date` for pre-releases, where:

- **version** specifies the version of the release:
    - `1.0.0`
    - `2.3.0`
- **stage** specifies the release stage:
    - `alpha` - represents early iterations that may be unstable or incomplete.
    - `beta` - represents a feature-complete version but may still contain some bugs.
- **n** prerelease increment (optional):
    - `1` - first alpha prerelease
    - `2` - second alpha prerelease
- **date** specifies the date of the pre-release (optional):
    - `250101`
    - `20250101`

For example:

| Stage   | Version                |
|---------|------------------------|
| Alpha   | 1.0.0-alpha            |
| Alpha   | 1.0.0-alpha.1.20250101 |
| Beta    | 1.0.0-beta.2.20250101  |
| Release | 1.0.0                  |

### Updating the Changelog

For releases, changelog for the release version is required.

To update the changelog:

1. Navigate to the solution root.
2. Open the file **Changelog.md**.
3. Add a section for your version. The version separator is the `#` symbol.
4. Specify the release number e.g. `# 1.0.0` or `# 25.01.01 v1.0.0`, the format does not matter, the main thing is that it contains the version.
5. In the lines below, write a changelog for your version, style to your taste.
6. Commit your changes.

### Creating a new Release from the JetBrains Rider

JetBrains provides a handy UI for creating a tag, it can be created in a few steps:

1. Open JetBrains Rider.
2. Navigate to the **Git** tab.
3. Click **New Tag...** and create a new tag.

   ![image](https://github.com/user-attachments/assets/19c11322-9f95-45e5-8fe6-defa36af59c4)

4. Navigate to the **Git** panel.
5. Expand the **Tags** section.
6. Right-click on the newly created tag and select **Push to origin**.

   ![image](https://github.com/user-attachments/assets/b2349264-dd76-4c21-b596-93110f1f16cb)

   This process will trigger the Release workflow and create a new Release on GitHub.

### Creating a new Release from the Terminal

Alternatively, you can create and push tags using the terminal:

1. Navigate to the repository root and open the terminal.
2. Use the following command to create a new tag:
   ```shell
   git tag 'version'
   ```

   Replace `version` with the desired version, e.g., `1.0.0`.
3. Push the newly created tag to the remote repository using the following command:
   ```shell
   git push origin 'version'
   ```

> [!NOTE]  
> The tag will reference your current commit, so verify you're on the correct branch and have fetched latest changes from remote first.

### Creating a new Release on GitHub

To create releases directly on GitHub:

1. Navigate to the **Actions** section on the repository page.
2. Select **Publish Release** workflow.
3. Click **Run workflow** button.
4. Specify the release version and click **Run**.

    ![image](https://github.com/user-attachments/assets/088388c1-6055-4d21-8d22-70f047d8f104)


## Compiling a solution on GitHub

Pushing commits to the remote repository will start a pipeline compiling the solution for all specified Revit versions. 
That way, you can check if the plugin is compatible with different API versions without having to spend time building it locally.

## Conditional compilation for a specific Revit version

To write code compatible with different Revit versions, use the directives **#if**, **#elif**, **#else**, **#endif**.

```c#
#if REVIT2026
    //Your code here
#endif
```

To target a specific Revit version, set the solution configuration in your IDE interface to match that version.
E.g., select the `Debug R26` configuration for the Revit 2026 API.

The project has available constants such as `REVIT2026`, `REVIT2026_OR_GREATER`, `REVIT2026_OR_GREATER`.
Create conditions, experiment to achieve the desired result.

> [!NOTE]  
> For generating directives, a third-party package is used.
> You can find more detailed documentation about it here: [Revit.Build.Tasks](https://github.com/Nice3point/Revit.Build.Tasks)

To support the latest APIs in legacy Revit versions:

```c#
#if REVIT2021_OR_GREATER
    UnitUtils.ConvertFromInternalUnits(69, UnitTypeId.Millimeters);
#else
    UnitUtils.ConvertFromInternalUnits(69, DisplayUnitType.DUT_MILLIMETERS);
#endif
```

`#if REVIT2021_OR_GREATER` сompiles a block of code for Revit versions 21, 22, 23 and greater.

To support removed APIs in newer versions of Revit, you can invert the constant:

```c#
#if !REVIT2023_OR_GREATER
    var builtinCategory = (BuiltInCategory) category.Id.IntegerValue;
#endif
```

`#if !REVIT2023_OR_GREATER` сompiles a block of code for Revit versions 22, 21, 20 and lower.

## Managing Supported Revit Versions

To extend or reduce the range of supported Revit API versions, you need to update the solution and project configurations.

### Solution configurations

Solution configurations determine which projects are built and how they are configured.

To support multiple Revit versions:
- Open the `.sln` file.
- Add or remove configurations for each Revit version.

Example:

```text
GlobalSection(SolutionConfigurationPlatforms) = preSolution
    Debug R24|Any CPU = Debug R24|Any CPU
    Debug R25|Any CPU = Debug R25|Any CPU
    Debug R26|Any CPU = Debug R26|Any CPU
    Release R24|Any CPU = Release R24|Any CPU
    Release R25|Any CPU = Release R25|Any CPU
    Release R26|Any CPU = Release R26|Any CPU
EndGlobalSection
```

For example `Debug R26` is the Debug configuration for Revit 2026 version.

> [!TIP]  
> If you are just ending maintenance for some version, removing the Solution configurations without modifying the Project configurations is enough.

### Project configurations

Project configurations define build conditions for specific versions.

To add or remove support:
- Open `.csproj` file
- Add or remove configurations for Debug and Release builds.

Example:

```xml
<PropertyGroup>
    <Configurations>Debug R24;Debug R25;Debug R26</Configurations>
    <Configurations>$(Configurations);Release R24;Release R25;Release R26</Configurations>
</PropertyGroup>
```

> [!IMPORTANT]  
> Edit the `.csproj` file only manually, IDEs often break configurations.

Then simply map the solution configuration to the project configuration:

![image](https://github.com/user-attachments/assets/9f357ded-d38c-4f0a-a21f-152de75f4abc)

Solution and project configuration names may differ, this example uses the same naming style to avoid confusion.

Then specify the framework and Revit version for each configuration, update the `.csproj` file with the following:

```xml
<PropertyGroup Condition="$(Configuration.Contains('R26'))">
    <RevitVersion>2026</RevitVersion>
    <TargetFramework>net8.0-windows</TargetFramework>
</PropertyGroup>
```

## API references

To support CI/CD pipelines and build a project for Revit versions not installed on your computer, use Nuget packages.

> [!NOTE]  
> Revit API dependencies are available in the [Revit.API](https://github.com/Nice3point/RevitApi) repository.

The Nuget package version must include wildcards `Version="$(RevitVersion).*"` to automatically include adding a specific package version, depending on the selected solution
configuration.

```xml
<ItemGroup>
    <PackageReference Include="Nice3point.Revit.RevitAPI" Version="$(RevitVersion).*"/>
    <PackageReference Include="Nice3point.Revit.RevitAPIUI" Version="$(RevitVersion).*"/>
</ItemGroup>
```

## Learn More

* You can explore more in the [RevitTemplates Wiki](https://github.com/Nice3point/RevitTemplates/wiki) page.