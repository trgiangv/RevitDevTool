# net48 Compatibility Review

Use when editing shared libraries or code reachable from Autodesk 2022-2024.

## Checklist

- Confirm whether the touched code compiles for `net48`.
- Avoid APIs unavailable on .NET Framework unless the repo already has a helper, package, or conditional path.
- Check `Directory.Build.props`, target framework conditions, and package conditions before using newer BCL APIs.
- Avoid assuming collectible `AssemblyLoadContext` exists in .NET Framework paths.
- Build at least `scripts/agent/build-host.ps1 -Year 2024` when compatibility is relevant.
- If the build cannot run, state the missing SDK or environment blocker.
