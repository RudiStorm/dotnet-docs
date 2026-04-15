using System.Text.Json.Serialization;

namespace DotNetDocs.Core;

public enum SymbolKind
{
    Unknown,
    Type,
    Method,
    Property,
    Field,
    Event
}

public enum ResolutionMode
{
    None,
    CanonicalUrl,
    Search,
    Cache,
    OfflineCache
}

public enum ResolutionStatus
{
    Success,
    NotFound,
    Ambiguous,
    Invalid,
    NetworkError,
    Error
}

public sealed record SymbolQuery(
    string OriginalText,
    string NormalizedText,
    string SearchText,
    string? NamespaceName,
    string TypeName,
    string? MemberName,
    int GenericArity,
    bool AliasExpanded,
    bool IsFullyQualified,
    IReadOnlyList<string> CandidateSymbols,
    IReadOnlyList<string> Diagnostics);

public sealed record SearchCandidate(
    string Symbol,
    SymbolKind Kind,
    string? Namespace,
    string? Summary,
    string Url,
    double Score,
    string Source);

public sealed record DocumentationRecord(
    string Symbol,
    SymbolKind Kind,
    string? Namespace,
    string? Summary,
    IReadOnlyList<string> Signatures,
    int? OverloadCount,
    string Url,
    string? AssemblyName,
    IReadOnlyList<SearchCandidate> Overloads,
    IReadOnlyList<string> Diagnostics);

public sealed record DocumentationResult(
    string Query,
    string NormalizedQuery,
    ResolutionMode ResolutionMode,
    ResolutionStatus Status,
    DocumentationRecord? Record,
    IReadOnlyList<SearchCandidate> Candidates,
    IReadOnlyList<string> Diagnostics)
{
    public bool IsSuccess => Status == ResolutionStatus.Success && Record is not null;
}

public sealed record DocumentationOptions(
    bool ForceSearch,
    bool IncludeOverloads,
    bool DisableCache,
    TimeSpan CacheTtl,
    string View,
    bool IncludeDiagnostics = false);

public sealed record JsonOutputModel(
    [property: JsonPropertyName("query")] string Query,
    [property: JsonPropertyName("normalized_query")] string NormalizedQuery,
    [property: JsonPropertyName("resolution_mode")] string ResolutionMode,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("symbol")] string? Symbol,
    [property: JsonPropertyName("kind")] string? Kind,
    [property: JsonPropertyName("namespace")] string? Namespace,
    [property: JsonPropertyName("signatures")] IReadOnlyList<string> Signatures,
    [property: JsonPropertyName("summary")] string? Summary,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("assembly")] string? Assembly,
    [property: JsonPropertyName("overload_count")] int? OverloadCount,
    [property: JsonPropertyName("candidates")] IReadOnlyList<SearchCandidate> Candidates,
    [property: JsonPropertyName("diagnostics")] IReadOnlyList<string> Diagnostics);
