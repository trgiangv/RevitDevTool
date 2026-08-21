# TUnit Host Testing

RevitDevTool runs TUnit tests through the same TestRunner and neutral
`testing/run` IPC path used by NUnit. TUnit is a framework-specific provider;
it does not launch or activate Revit.

Last updated: 2026-08-21

## Supported matrix

| Revit | Target framework | TUnit |
|---|---|---|
| 2023 | `net48` | 1.65.38 |
| 2025 | `net8.0-windows` | 1.65.38 |

Other Revit versions, AutoCAD, Civil 3D, and `net10` are rejected for TUnit.
NUnit remains the default framework.

## Test project

```xml
<PropertyGroup>
  <UseRevit>true</UseRevit>
  <IsTestProject>true</IsTestProject>
  <TestingFramework>tunit</TestingFramework>
  <HostName>Revit</HostName>
  <HostVersion>$(RevitVersion)</HostVersion>
</PropertyGroup>
<ItemGroup>
  <PackageReference Include="Microsoft.Testing.Platform.MSBuild" />
  <PackageReference Include="TUnit" Version="1.65.38" />
  <PackageReference Include="RevitDevTool.TestAdapter" />
</ItemGroup>
```

## Runtime behavior

- The outer adapter performs host-free discovery from TUnit's generated test
  entries, then sends the selected TUnit UIDs to TestRunner.
- TestRunner retains sole ownership of locating, reusing, or starting Revit and
  sends the neutral `testing/run` request over the existing IPC pipe.
- `MarshaledTestingRequestHandler` enters the existing `IHostContextExecutor`;
  there is no TUnit dispatcher, synchronization context, host launcher, or new
  activation request.
- The `tunit` host provider uses the existing generation store/session manager.
  TUnit and MTP load from `TUnitRuntime` plus the immutable test generation,
  outside the merged add-in root.
- Revit 2025 uses a collectible load context. Revit 2023 uses the existing
  scoped .NET Framework isolation and exact manifest identity resolution.
- The net48 payload carries the exact dependency identities required by TUnit
  and MTP, including side-by-side `System.Text.Json` identities.
- The current spike supports source-generated, non-data-driven TUnit tests.
  Data-source UID expansion and cooperative cancellation remain follow-up work.

See `samples/DevTools.TUnit.SampleTests` for the supported project shape.
