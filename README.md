# RevitDevTool

Autodesk Revit plugin project organized into multiple solution files that target versions 2021 - 2026.

## Table of content

<!-- TOC -->
- [RevitDevTool](#revitdevtool)
  - [Table of content](#table-of-content)
  - [Usage](#usage)
    - [🎯 Getting Started](#-getting-started)
    - [📊 Trace Log](#-trace-log)
      - [Features](#features)
      - [How to Use](#how-to-use)
      - [For Python/IronPython Users](#for-pythonironpython-users)
    - [🎨 Trace Geometry - Beautiful 3D Visualization](#-trace-geometry---beautiful-3d-visualization)
      - [Supported Geometry Types](#supported-geometry-types)
      - [Key Features](#key-features)
      - [How to Use Geometry Visualization](#how-to-use-geometry-visualization)
      - [Python/IronPython Geometry Visualization](#pythonironpython-geometry-visualization)
    - [🛠️ Advanced Usage Examples](#️-advanced-usage-examples)
      - [Example 1: Debugging Element Geometry](#example-1-debugging-element-geometry)
      - [Example 2: Visualizing Analysis Results](#example-2-visualizing-analysis-results)
      - [Example 3: Python Script Integration](#example-3-python-script-integration)
    - [🎛️ User Interface Features](#️-user-interface-features)
    - [🔧 Language Support](#-language-support)
    - [💡 Best Practices](#-best-practices)
    - [🔍 Troubleshooting](#-troubleshooting)
  - [Screenshots](#screenshots)
  - [Prerequisites](#prerequisites)
  - [Solution Structure](#solution-structure)
  - [Project Structure](#project-structure)
  - [Building](#building)
    - [Building the MSI installer and the Autodesk bundle on local machine](#building-the-msi-installer-and-the-autodesk-bundle-on-local-machine)
  - [Publishing Releases](#publishing-releases)
    - [Updating the Changelog](#updating-the-changelog)
    - [Creating a new Release from the JetBrains Rider](#creating-a-new-release-from-the-jetbrains-rider)
    - [Creating a new Release from the Terminal](#creating-a-new-release-from-the-terminal)
    - [Creating a new Release on GitHub](#creating-a-new-release-on-github)
  - [Compiling a solution on GitHub](#compiling-a-solution-on-github)
  - [Conditional compilation for a specific Revit version](#conditional-compilation-for-a-specific-revit-version)
  - [Managing Supported Revit Versions](#managing-supported-revit-versions)
    - [Solution configurations](#solution-configurations)
    - [Project configurations](#project-configurations)
  - [API references](#api-references)
  - [Learn More](#learn-more)
<!-- TOC -->

## Usage

RevitDevTool is a comprehensive debugging and visualization toolkit for Autodesk Revit that helps developers and users trace, visualize, and debug their Revit applications. The tool provides two main capabilities: **Trace Logging** and **Geometry Visualization** with beautiful canvas rendering.

### 🎯 Getting Started

1. **Install RevitDevTool**: Use the MSI installer or place the Autodesk bundle in your Revit add-ins folder
2. **Launch Revit**: The tool will automatically register and appear in the **External Tools** panel
3. **Open Trace Panel**: Click the **"Trace Panel"** button in the ribbon to show/hide the dockable panel

### 📊 Trace Log

The Trace Log provides a real-time, color-coded logging interface directly within Revit, similar to Visual Studio's output window.

#### Features

- **Real-time logging** with configurable log levels (Debug, Information, Warning, Error, Fatal)
- **Color-coded output** for easy identification of different log types
- **Console redirection** - captures `Console.WriteLine()` output
- **Auto-start listening** on Revit startup
- **Cascadia Mono font** for better readability
- **Clear function** to reset the log output

#### How to Use

1. **Enable Logging**: Open the `Trace Log` DockablePanel and ensure the logger is `enabled`
2. **Configure Log Level**: Select your desired minimum log level from the dropdown (Debug, Information, Warning, Error, Fatal)
3. **Start Logging**: Use any of the following methods in your C# code:

```csharp
// Information logging
Trace.TraceInformation("Application started successfully");

// Warning logging  
Trace.TraceWarning("Element not found, using default values");

// Error logging
Trace.TraceError("Failed to process element: " + exception.Message);

// Debug logging
Trace.TraceData(TraceEventType.Verbose, 0, "Debug data: " + debugInfo);

// Console output (automatically captured)
Console.WriteLine("This will appear in the trace log");
```

#### For Python/IronPython Users

```python
import clr
clr.AddReference("System")
from System.Diagnostics import Trace

# Use the same Trace methods
Trace.TraceInformation("Python script executed")
Trace.TraceWarning("Warning from Python")
Trace.TraceError("Error in Python script")

# Console output is also captured
print("This will appear in the trace log")
```

### 🎨 Trace Geometry - Beautiful 3D Visualization

The Geometry Visualization system allows you to display transient geometry directly in the Revit 3D view, similar to Dynamo's preview functionality but integrated into your development workflow.

#### Supported Geometry Types

- **Faces** - Surface geometry from Revit elements
- **Curves** - Lines, arcs, splines, and complex curve geometry  
- **Solids** - 3D solid geometry with volume
- **Meshes** - Triangulated mesh geometry
- **Points (XYZ)** - Individual points or point collections
- **Bounding Boxes** - Element bounding box visualization
- **Collections** - Multiple geometry objects at once

#### Key Features

- **Transient Display** - Geometry appears as temporary, non-selectable objects
- **Automatic Cleanup** - Geometry is removed when document closes
- **Manual Control** - Use `ClearTraceGeometry` to remove geometry on demand
- **Document-specific** - Each document maintains its own geometry collection
- **Performance Optimized** - Uses Revit's internal transient display methods

#### How to Use Geometry Visualization

1. **Enable Geometry Tracing**: Ensure the logger is started (geometry tracing is automatically enabled)

2. **Trace Single Geometry Objects**:

```csharp
// Trace a face
Face face = GetSomeFace();
Trace.Write(face);

// Trace a curve
Curve curve = GetSomeCurve();
Trace.Write(curve);

// Trace a solid
Solid solid = GetSomeSolid();
Trace.Write(solid);

// Trace a mesh
Mesh mesh = GetSomeMesh();
Trace.Write(mesh);

// Trace a point
XYZ point = new XYZ(10, 20, 30);
Trace.Write(point);

// Trace a bounding box
BoundingBoxXYZ bbox = element.get_BoundingBox(null);
Trace.Write(bbox);
```

3. **Trace Multiple Geometry Objects**:

```csharp
// Trace multiple faces
var faces = new List<Face> { face1, face2, face3 };
Trace.Write(faces);

// Trace multiple curves
var curves = selectedElements.SelectMany(e => GetCurvesFromElement(e));
Trace.Write(curves);

// Trace multiple solids
var solids = elements.SelectMany(e => e.GetSolids());
Trace.Write(solids);

// Mixed geometry types
var geometries = new List<GeometryObject> { face, curve, solid };
Trace.Write(geometries);
```

4. **Clear Traced Geometry**:

```csharp
// Clear all traced geometry for the current document
// Use the "Clear Geometry" button in the UI, or programmatically:
// (This happens automatically via the UI button)
```

#### Python/IronPython Geometry Visualization

```python
import clr
clr.AddReference("RevitAPI")
clr.AddReference("System")

from Autodesk.Revit.DB import *
from System.Diagnostics import Trace
from System.Collections.Generic import List

# Trace individual geometry
face = GetSomeFace()  # Your method to get a face
Trace.Write(face)

# Trace collections
curves = List[Curve]()
curves.Add(curve1)
curves.Add(curve2)
Trace.Write(curves)

# Trace points
point = XYZ(10, 20, 30)
Trace.Write(point)
```

### 🛠️ Advanced Usage Examples

#### Example 1: Debugging Element Geometry

```csharp
foreach (Element element in selectedElements)
{
    Trace.TraceInformation($"Processing element: {element.Id}");
    
    var solids = element.GetSolids();
    if (solids.Any())
    {
        Trace.Write(solids);
        Trace.TraceInformation($"Found {solids.Count} solids");
    }
    else
    {
        Trace.TraceWarning($"No solids found for element {element.Id}");
    }
}
```

#### Example 2: Visualizing Analysis Results

```csharp
// Visualize structural analysis results
var analysisPoints = CalculateStressPoints(beam);
Trace.Write(analysisPoints);

// Show critical areas
var criticalFaces = GetCriticalFaces(beam);
Trace.Write(criticalFaces);

Trace.TraceInformation($"Analysis complete: {analysisPoints.Count} points analyzed");
```

#### Example 3: Python Script Integration

```python
# Python script for geometry analysis
import clr
clr.AddReference("RevitAPI")
from Autodesk.Revit.DB import *
from System.Diagnostics import Trace

def analyze_walls(walls):
    Trace.TraceInformation("Starting wall analysis...")
    
    for wall in walls:
        # Get wall geometry
        geometry = wall.get_Geometry(Options())
        
        for geo_obj in geometry:
            if isinstance(geo_obj, Solid) and geo_obj.Volume > 0:
                # Visualize the solid
                Trace.Write(geo_obj)
                
                # Log information
                Trace.TraceInformation(f"Wall {wall.Id}: Volume = {geo_obj.Volume}")

# Usage
selected_walls = [doc.GetElement(id) for id in uidoc.Selection.GetElementIds()]
analyze_walls(selected_walls)
```

### 🎛️ User Interface Features

- **Dockable Panel**: Integrated seamlessly with Revit's interface
- **Responsive Design**: Works with Revit's light and dark themes (2024+)
- **Resizable**: Minimum 300x400 pixels, can be resized as needed
- **Keyboard Shortcuts**: Standard copy/paste functionality in the log view
- **Auto-scroll**: Automatically scrolls to show new log entries
- **Log Level Filtering**: Real-time filtering of log messages

### 🔧 Language Support

RevitDevTool works with multiple programming languages and scripting environments:

- **C#** - Full support through .NET Trace API
- **VB.NET** - Full support through .NET Trace API  
- **Python/IronPython** - Full support via pyRevit, RevitPythonShell, or IronPython scripts
- **F#** - Support through .NET interop
- **Any .NET Language** - Works with any language that can access System.Diagnostics.Trace

### 💡 Best Practices

1. **Use Appropriate Log Levels**: Use Information for general status, Warning for non-critical issues, Error for problems
2. **Clear Geometry Regularly**: Use the Clear Geometry button to avoid cluttering the view
3. **Meaningful Messages**: Include element IDs, counts, and relevant context in log messages  
4. **Performance Considerations**: Large geometry collections may impact performance
5. **Document Workflow**: Use logging to document your script's progress and results

### 🔍 Troubleshooting

- **No Geometry Visible**: Ensure the logger is enabled and you're viewing the correct 3D view
- **Geometry Persists**: Use "Clear Geometry" button or close/reopen the document
- **Missing Logs**: Check that the log level is set appropriately for your trace calls
- **Performance Issues**: Reduce the number of geometry objects traced simultaneously

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
from [JetBrains Rider website](https://www.jetbrains.com/rider/).

1. Open JetBrains Rider
2. In the `Solutions Configuration` drop-down menu, select `Release R25` or `Debug R25`. Suffix `R25` means compiling for the Revit 2025.
3. After the solution loads, you can build it by clicking on `Build -> Build Solution`.
4. `Debug` button will start Revit add-in in the debug mode.

   ![image](https://github.com/user-attachments/assets/d209d863-a6d5-43a9-83e1-5eeb2b9fddac)

Also, you can use Visual Studio. If you don't have Visual Studio installed, download it from [Visual Studio Downloads](https://visualstudio.microsoft.com/downloads/).

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
3. **Run the build**. Once you have navigated to your project's root directory, you can run the NUKE build by calling:   Compile:

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
4. Specify the release version and click **Run**.    ![image](https://github.com/user-attachments/assets/088388c1-6055-4d21-8d22-70f047d8f104)

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

- You can explore more in the [RevitTemplates Wiki](https://github.com/Nice3point/RevitTemplates/wiki) page.
