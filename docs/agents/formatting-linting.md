# Formatting and linting

`.editorconfig` is the shared source for C# and whitespace conventions. Rider reads these settings directly. The local .NET tool manifest pins XamlStyler and `Settings.XamlStyler` defines XAML formatting.

## Naming rules

Enum members are exempt. Vendored code is excluded from the rule: `libs/.editorconfig` sets `IDE1006` to `none`, so `dotnet build` does not fail on HandyControl.

## Solution-wide runs

`scripts/format-lint.ps1` formats only changed files. When running the tools by hand across the solution, exclude vendored code:

```powershell
dotnet format whitespace RevitDevTool.slnx --verify-no-changes --exclude libs/ --no-restore
dotnet format style RevitDevTool.slnx --verify-no-changes --exclude libs/ --no-restore
```

Severity settings in `.editorconfig` silence diagnostics; they do not exclude files from `dotnet format`, and `RevitDevTool.slnx` references the two `libs/` projects. Without `--exclude libs/` the whitespace and style passes report HandyControl code.

## Rider

To format files when saving, open **Settings | Tools | Actions on Save**, enable **Reformat and Cleanup Code**, select **Built-in: Reformat Code**, and choose **Changed lines**. In **Settings | Editor | Code Style | C# | Line Breaks and Wrapping**, enable **Wrap long lines** and set the right margin to 120; `.editorconfig` sets that margin and the chain/argument wrapping rules. Rider also reformats C# while typing and can format a file with `Ctrl+Alt+L`.

For diagnostics, use Rider's inspections and the .NET SDK code analyzers during MSBuild. `Directory.Build.props` enables the SDK analyzers at analysis level 10.0 in Default mode, including for `net48`, and enforces configured code-style rules during build. Keep `.editorconfig` as the source of formatting, naming, and analyzer severity rules.

## Agents

Agents run `scripts/format-lint.ps1` once, after the task's edits are finished. There is no per-edit hook: `dotnet format` reloads the project on every call, so formatting after each edit cost about 25 s per C# edit, while one run at the end covers all of them.

```powershell
pwsh -File scripts/format-lint.ps1
pwsh -File scripts/format-lint.ps1 -Paths source/DevTools.Utilities/SomeFile.cs
pwsh -File scripts/format-lint.ps1 -All
```

Without arguments it takes the changed and untracked `.cs` and `.xaml` files from `git status` under `source/`, `tests/`, and `build/` (never `bin`/`obj`). C# files are grouped by their nearest project; each project gets one `dotnet format whitespace`, one `dotnet format style` (warning severity and above), and one `--verify-no-changes` style pass. Anything the formatter cannot fix, such as an `IDE1006` naming violation, is listed and the script exits 1. XAML files use the pinned local XamlStyler tool. The script does not build; compile proof stays with `.agents/skills/build/SKILL.md`.

Style rules without a severity in `.editorconfig` (for example `dotnet_style_qualification_for_field = false`) are silent, so the script neither fixes nor reports them.

Host projects (`UseRevit`/`UseAutoCad`) load only under an Autodesk configuration, so their files are formatted under `Debug.Autodesk.2025` after a restore, which is slower. `-All` formats every `.cs`/`.xaml` under those folders through `RevitDevTool.slnx` (about 7 minutes): some net48 test projects load only under the default configuration, so it restores and formats twice, `Debug.Autodesk.2025` then the default (this leaves `obj/` restored for normal builds).
