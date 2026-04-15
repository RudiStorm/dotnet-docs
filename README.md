# dotnet-docs

`dotnet-docs` is a terminal-first .NET tool for resolving official .NET API documentation without leaving your shell. It targets Microsoft Learn as the source of truth, uses `System.CommandLine` for the CLI surface, and keeps common lookups fast with a lightweight local cache.

## Why It Exists

Developers often know the symbol they want but do not want to context-switch into a browser just to confirm an overload, copy a URL, or sanity-check a namespace. `dotnet-docs` is meant to feel closer to `go doc`, while staying honest about how .NET tools and Microsoft Learn actually work.

## Install

Global tool:

```bash
dotnet tool install --global dotnet-docs
```

Local tool:

```bash
dotnet new tool-manifest
dotnet tool install --local dotnet-docs
dotnet tool run dotnet-docs string.join
```

## How To Run It

This project runs as a .NET tool named `dotnet-docs`.

If you published with ClickOnce into `C:\dev\csdocs\publish`, that output is not the format used to run or install this tool from the command line. For this project, use `dotnet pack` to create a NuGet tool package, then install that package as a .NET tool.

### Run from a packaged tool

Create the package:

```powershell
dotnet pack C:\dev\csdocs\src\dotnet-docs\DotNetDocs.Tool.csproj -c Release
```

Install it globally from the generated package:

```powershell
dotnet tool install --global --add-source C:\dev\csdocs\src\dotnet-docs\bin\Release dotnet-docs
```

Run it:

```powershell
dotnet-docs string.join
dotnet-docs System.String.Join
dotnet-docs Console.WriteLine --overloads
dotnet-docs Dictionary<TKey,TValue>.TryGetValue --url
dotnet-docs Enumerable.Select --json
```

### Run as a local tool

If you do not want a global install:

```powershell
dotnet new tool-manifest
dotnet tool install --local --add-source C:\dev\csdocs\src\dotnet-docs\bin\Release dotnet-docs
dotnet tool run dotnet-docs string.join
```

### Run during development

You can also run the project directly from source:

```powershell
dotnet run --project C:\dev\csdocs\src\dotnet-docs\DotNetDocs.Tool.csproj -- string.join
```

### Verify installation

Check whether the tool is installed:

```powershell
dotnet tool list --global
dotnet tool list --local
```

If `dotnet-docs` is installed globally, this should work:

```powershell
dotnet-docs --help
```

If PowerShell says `dotnet-docs` is not recognized, the tool is either not installed yet or your install step used ClickOnce instead of `dotnet tool install`.

### Quick update script

After changes to the tool, you can repack and reinstall it with:

```powershell
pwsh -File C:\dev\csdocs\scripts\Update-DotNetDocsTool.ps1
```

For a local tool install instead of a global one:

```powershell
pwsh -File C:\dev\csdocs\scripts\Update-DotNetDocsTool.ps1 -Scope local
```

To repack, reinstall, and immediately verify the command:

```powershell
pwsh -File C:\dev\csdocs\scripts\Update-DotNetDocsTool.ps1 -SmokeTest
```

## Usage

Primary command:

```bash
dotnet-docs string.join
dotnet-docs System.String.Join
dotnet-docs Console.WriteLine --overloads
dotnet-docs Enumerable.Select --json
dotnet-docs Dictionary<TKey,TValue>.TryGetValue --url
```

Equivalent explicit subcommand:

```bash
dotnet-docs docs string.join
```

Cache helpers:

```bash
dotnet-docs cache path
dotnet-docs cache clear
```

## Command Shape Limitation

This project ships as a standard .NET tool command named `dotnet-docs`. That is the clean, supported install shape for third-party tools. For local tools, the supported `dotnet` integration is `dotnet tool run <command>`, not arbitrary first-party-looking verbs. In practice that means `dotnet docs ...` should be treated as a shell alias goal rather than a packaging guarantee.

Example aliases:

```powershell
function dotnet-docs-go { dotnet-docs @args }
Set-Alias ddocs dotnet-docs
```

```bash
alias ddocs='dotnet-docs'
```

## Features In v1

- Canonical Microsoft Learn URL resolution for fully qualified symbols.
- Official Microsoft Learn search fallback for shorthand queries.
- C# alias expansion such as `string`, `int`, `bool`, `Console`, `List<T>`, and `Dictionary<TKey,TValue>`.
- Compact terminal rendering.
- `--open`, `--url`, `--json`, `--search`, `--overloads`, and cache controls.
- Local file-based caching with stale-cache fallback during network failures.

## Architecture

- `src/dotnet-docs`: CLI entrypoint and command definitions.
- `src/dotnet-docs.Core`: normalization, contracts, shared models, and ranking.
- `src/dotnet-docs.Providers.MicrosoftLearn`: Microsoft Learn URL resolution, official search integration, parsing, and cache implementation.
- `src/dotnet-docs.Rendering`: terminal and JSON output formatting.
- `tests/dotnet-docs.Tests`: unit and provider integration tests.

## Provider Strategy

Resolution prefers:

1. Canonical Microsoft Learn API URLs for fully qualified symbols.
2. Official Microsoft Learn search results when canonical resolution is not enough.
3. Alias and shorthand expansion before search.

Because Microsoft Learn does not expose a simple stable symbol-metadata API tailored to this CLI scenario, the provider keeps HTTP access behind interfaces and limits HTML parsing to the minimum required metadata extraction.

## Development

```bash
dotnet restore
dotnet build
dotnet test
```

Pack the tool:

```bash
dotnet pack src/dotnet-docs/DotNetDocs.Tool.csproj -c Release
```

## Limitations

- Search quality depends on the current Microsoft Learn index.
- Signature extraction is intentionally conservative until a richer official metadata source is available.
- Offline mode only supports previously cached lookups.

## Roadmap

- v1.1: richer overload rendering, better fuzzy ranking, and shell completions.
- v2: offline cache enrichment, editor integration hooks, REPL mode, and optional terminal UI.
