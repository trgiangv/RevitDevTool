---
name: revit-build
description: Build and test Revit add-ins with correct configurations for Revit 2024 (NET 4.8) and Revit 2025 (NET 8.0). Use when building, compiling, testing Revit projects, or when the user mentions Revit versions, build configurations, or testing across multiple Revit versions.
---

# Revit Build Configuration

## Overview

This project supports multiple Revit versions with different .NET frameworks:
- **Revit 2022-2024**: .NET Framework 4.8 (`net48`)
- **Revit 2025-2026**: .NET 8.0 (`net8.0-windows`)

## Build Configurations

The project uses configuration-based targeting defined in `source/Directory.Build.props`:

| Configuration | Revit Version | Target Framework |
|---------------|---------------|------------------|
| `Debug R22` / `Release R22` | 2022 | net48 |
| `Debug R23` / `Release R23` | 2023 | net48 |
| `Debug R24` / `Release R24` | 2024 | net48 |
| `Debug R25` / `Release R25` | 2025 | net8.0-windows |
| `Debug R26` / `Release R26` | 2026 | net8.0-windows |

## Building for Testing

### Build for Revit 2024 and 2025

When testing, always build both Revit 2024 and 2025 configurations:

```bash
# Build for Revit 2024 (NET 4.8)
dotnet build RevitDevTool.sln -c "Release R24"

# Build for Revit 2025 (NET 8.0)
dotnet build RevitDevTool.sln -c "Release R25"
```

### Build All Configurations

To build all Release configurations (recommended for comprehensive testing):

```bash
dotnet build RevitDevTool.sln -c "Release R22"
dotnet build RevitDevTool.sln -c "Release R23"
dotnet build RevitDevTool.sln -c "Release R24"
dotnet build RevitDevTool.sln -c "Release R25"
dotnet build RevitDevTool.sln -c "Release R26"
```

### Using NUKE Build System

The project uses NUKE for build automation. To compile all configurations:

```bash
# Windows
.\.nuke\build.cmd Compile

# Or directly
dotnet run --project build/Build.csproj -- Compile
```

The NUKE build automatically compiles all `Release*` configurations as defined in `build/Build.Configuration.cs`.

## Key Configuration Files

### Directory.Build.props

Located at `source/Directory.Build.props`, this file:
- Maps configuration names (R22, R23, R24, R25, R26) to Revit versions
- Sets the appropriate `TargetFramework` based on configuration
- Defines the `$(RevitVersion)` property used throughout the project

```xml
<PropertyGroup Condition="$(Configuration.Contains('R24'))">
    <RevitVersion>2024</RevitVersion>
    <TargetFramework>net48</TargetFramework>
</PropertyGroup>
<PropertyGroup Condition="$(Configuration.Contains('R25'))">
    <RevitVersion>2025</RevitVersion>
    <TargetFramework>net8.0-windows</TargetFramework>
</PropertyGroup>
```

### Project Files

Both `RevitDevTool.csproj` and `RevitDevTool.Test.csproj`:
- Import `Directory.Build.props` automatically
- Use `$(RevitVersion)` to reference version-specific NuGet packages
- Automatically target the correct framework based on configuration

## Testing Workflow

When running tests or validating changes:

1. **Build both primary versions**:
   - Revit 2024 (NET 4.8) - represents the .NET Framework era
   - Revit 2025 (NET 8.0) - represents the modern .NET era

2. **Check for framework-specific issues**:
   - API differences between Revit versions
   - .NET Framework vs .NET 8.0 compatibility
   - NuGet package version compatibility

3. **Verify output directories**:
   - Builds output to configuration-specific directories
   - Each configuration has isolated dependencies

## Common Build Commands

```bash
# Clean all build artifacts
dotnet clean RevitDevTool.sln

# Restore NuGet packages
dotnet restore RevitDevTool.sln

# Build specific configuration
dotnet build RevitDevTool.sln -c "Release R24"

# Build with verbosity for troubleshooting
dotnet build RevitDevTool.sln -c "Release R24" -v detailed

# Run NUKE build (all configurations)
.\.nuke\build.cmd Compile
```

## Framework-Specific Considerations

### .NET Framework 4.8 (Revit 2022-2024)
- Uses traditional .NET Framework APIs
- Some modern C# features require `PolySharp` package
- Windows-only by design

### .NET 8.0 (Revit 2025-2026)
- Modern .NET runtime
- Better performance and memory management
- Native support for latest C# features
- Still Windows-only due to Revit dependency

## Troubleshooting

### Build fails for specific configuration
- Check that the corresponding Revit API packages are available
- Verify NuGet package sources are configured
- Ensure `Directory.Build.props` is not modified incorrectly

### Framework mismatch errors
- Confirm you're using the correct configuration name (R24, R25, etc.)
- Check that `TargetFramework` in build output matches expectations
- Verify no hardcoded framework references in project files

### Missing Revit API references
- The project uses `Nice3point.Revit.Api.*` packages
- These are version-specific and pulled via `VersionOverride="$(RevitVersion).*"`
- Ensure NuGet restore completed successfully
