# Changelog

All notable changes to `RevitDevTool.TestAdapter` are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).

## [Unreleased]

## [0.3.0]

### Changed

- The TestRunner ships inside the package under `tools/runner`. The adapter no longer looks for a runner in the RevitDevTool install folder.
- The NUnit, TUnit, and MSTest runtimes come from the RevitDevTool add-in. The package keeps only the test host side.
- The framework versions the runtimes were built against are `NUnit` 5.0.0, `TUnit` 1.81.10, and `MSTest` 4.5.2. A newer version with the same major works.

### Requirements

- The RevitDevTool `4.0.0-beta.8` add-in or later. An older add-in stops the run with a sentence that says to update it.
- The .NET 10 runtime or later on the machine that runs `dotnet test`.
