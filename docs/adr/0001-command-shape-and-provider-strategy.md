# ADR 0001: Command Shape And Provider Strategy

## Status

Accepted

## Context

The product goal is a terminal-native .NET documentation lookup tool distributed through the .NET tool ecosystem. Two questions matter early:

1. Can this realistically ship as `dotnet docs ...`?
2. What official-source strategy gives the best balance of correctness, speed, and maintainability?

## Decision

The installable command is `dotnet-docs`.

The CLI still supports an explicit `docs` subcommand internally so the surface can grow cleanly, but the supported installation shape is the tool command itself. If teams want `dotnet docs ...`, they should add a shell alias or wrapper script intentionally.

For documentation resolution, the provider uses this order:

1. Canonical Microsoft Learn API URL resolution for fully qualified symbols.
2. Official Microsoft Learn search fallback for shorthand or ambiguous input.
3. Alias and shorthand expansion before resolution.

The provider keeps HTTP access and caching behind interfaces. HTML parsing is used only for the minimum metadata extraction required to render useful terminal output, because the official documentation hub is Microsoft Learn but the symbol metadata access path is awkward for CLI use.

## Consequences

Positive:

- Aligns with the standard .NET tool distribution model.
- Keeps the command honest and cross-platform.
- Makes future provider replacement possible without rewriting the CLI.
- Gives fast warm lookups with lightweight local caching.

Tradeoffs:

- `dotnet docs ...` is not promised as a first-class installation experience.
- Search behavior depends on current Microsoft Learn indexing.
- Signature extraction is good enough for terminal use but not yet as rich as a dedicated metadata feed.
