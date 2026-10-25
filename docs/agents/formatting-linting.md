# Formatting and linting

`.editorconfig` is the shared source for C# and whitespace conventions. Rider reads these settings directly. The local .NET tool manifest pins XamlStyler and `Settings.XamlStyler` defines XAML formatting.

## Naming rules

Enum members are exempt. Vendored code is excluded from the rule: `libs/.editorconfig` sets `IDE1006` to `none`, so `dotnet build` does not fail on HandyControl.

## Solution-wide runs

The hooks format only changed files. When running the tools by hand across the solution, exclude vendored code:

```powershell
dotnet format whitespace RevitDevTool.slnx --verify-no-changes --exclude libs/ --no-restore
dotnet format style RevitDevTool.slnx --verify-no-changes --exclude libs/ --no-restore
```

Severity settings in `.editorconfig` silence diagnostics; they do not exclude files from `dotnet format`, and `RevitDevTool.slnx` references the two `libs/` projects. Without `--exclude libs/` the whitespace and style passes report HandyControl code.

## Rider

To format files when saving, open **Settings | Tools | Actions on Save**, enable **Reformat and Cleanup Code**, select **Built-in: Reformat Code**, and choose **Changed lines**. In **Settings | Editor | Code Style | C# | Line Breaks and Wrapping**, enable **Wrap long lines** and set the right margin to 120; `.editorconfig` sets that margin and the chain/argument wrapping rules. Rider also reformats C# while typing and can format a file with `Ctrl+Alt+L`.

For diagnostics, use Rider's inspections and the .NET SDK code analyzers during MSBuild. `Directory.Build.props` enables the SDK analyzers at analysis level 10.0 in Default mode, including for `net48`, and enforces configured code-style rules during build. Keep `.editorconfig` as the source of formatting, naming, and analyzer severity rules.

## Codex and Claude Code

The repo hooks run `scripts/format-lint-hook.ps1` after each successful edit/write. It handles only changed `.cs` and `.xaml` files under `source/`, `tests/`, and `build/` (never `bin`/`obj`). C# files get `dotnet format whitespace` and `dotnet format style` (warning severity and above) on the nearest project, then a `--verify-no-changes` style pass; anything left that the formatter cannot fix, such as an `IDE1006` naming violation, makes the hook exit 2 so the agent receives the diagnostics. XAML files use the pinned local XamlStyler tool. The hook does not build; compile proof stays with `.agents/skills/build/SKILL.md`.

Style rules without a severity in `.editorconfig` (for example `dotnet_style_qualification_for_field = false`) are silent, so the hook neither fixes nor reports them.

The script can also be run manually; without `-Paths` it reads the hook event JSON from standard input:

```powershell
pwsh -File scripts/format-lint-hook.ps1 -Paths source/DevTools.Utilities/SomeFile.cs
pwsh -File scripts/format-lint-hook.ps1 -All
```

`-All` formats every `.cs`/`.xaml` under the four folders through `RevitDevTool.slnx` (about 7 minutes). Host projects (`UseRevit`/`UseAutoCad`) only load under an Autodesk configuration and some net48 test projects only load under the default one, so it restores and formats twice: `Debug.Autodesk.2025`, then the default configuration (this leaves `obj/` restored for normal builds). For a single host-project file the hook also uses `Debug.Autodesk.2025` and restores first, so it takes longer (about 40 s).

Both hooks find the script relative to this repository. Codex may ask to trust the repo-local hook configuration before executing it; review the command in `/hooks` when prompted.
