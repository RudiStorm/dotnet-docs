# Contributing

## Local Setup

```bash
dotnet restore
dotnet build
dotnet test
```

The repo is organized as a small layered solution:

- `dotnet-docs`: CLI surface and exit-code policy.
- `dotnet-docs.Core`: symbol normalization, ranking, and shared contracts.
- `dotnet-docs.Providers.MicrosoftLearn`: Microsoft Learn provider plus cache implementation.
- `dotnet-docs.Rendering`: terminal and JSON formatting.

## Provider Notes

The Microsoft Learn provider intentionally wraps external calls behind `IDocumentationProvider` and `ICacheStore`. If a better official metadata endpoint becomes available later, it should slot into the provider layer without rewriting normalization or rendering.

## Ranking Notes

Ranking should prefer:

1. exact fully qualified symbol matches
2. exact simple type/member matches
3. alias-expanded matches
4. same namespace family matches
5. fuzzy search candidates

Any ranking changes should come with tests.

## Release Expectations

- Keep the tool installable as a standard .NET tool package.
- Update `CHANGELOG.md` for user-facing changes.
- Preserve stable JSON output fields whenever possible.
- Document command-shape or provider tradeoffs in `docs/adr`.
