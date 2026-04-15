using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DotNetDocs.Core;

namespace DotNetDocs.Providers.MicrosoftLearn;

public sealed class MicrosoftLearnProvider : IDocumentationProvider
{
    private readonly HttpClient _httpClient;
    private readonly ICacheStore _cacheStore;
    private readonly CandidateRanker _ranker;
    private readonly MicrosoftLearnUrlBuilder _urlBuilder;
    private readonly MicrosoftLearnPageParser _pageParser;

    public MicrosoftLearnProvider(
        HttpClient httpClient,
        ICacheStore cacheStore,
        CandidateRanker ranker,
        MicrosoftLearnUrlBuilder urlBuilder,
        MicrosoftLearnPageParser pageParser)
    {
        _httpClient = httpClient;
        _cacheStore = cacheStore;
        _ranker = ranker;
        _urlBuilder = urlBuilder;
        _pageParser = pageParser;
    }

    public async Task<DocumentationResult> ResolveAsync(SymbolQuery query, DocumentationOptions options, CancellationToken cancellationToken)
    {
        var cacheKey = $"{query.NormalizedText}|{options.View}|{options.IncludeOverloads}|{options.ForceSearch}";
        if (!options.DisableCache)
        {
            var cached = await _cacheStore.GetAsync<DocumentationResult>(cacheKey, options.CacheTtl, cancellationToken);
            if (cached is not null)
            {
                return cached with { ResolutionMode = ResolutionMode.Cache };
            }
        }

        try
        {
            DocumentationResult result;
            if (!options.ForceSearch)
            {
                result = await TryCanonicalUrlAsync(query, options, cancellationToken)
                    ?? await ResolveViaSearchAsync(query, options, cancellationToken);
            }
            else
            {
                result = await ResolveViaSearchAsync(query, options, cancellationToken);
            }

            if (!options.DisableCache && result.Status is ResolutionStatus.Success or ResolutionStatus.Ambiguous or ResolutionStatus.NotFound)
            {
                await _cacheStore.SetAsync(cacheKey, result, cancellationToken);
            }

            return result;
        }
        catch (HttpRequestException ex)
        {
            if (options.DisableCache)
            {
                return BuildError(query, ResolutionStatus.NetworkError, $"Network error: {ex.Message}");
            }

            var stale = await _cacheStore.GetStaleAsync<DocumentationResult>(cacheKey, cancellationToken);
            if (stale is not null)
            {
                return stale with
                {
                    ResolutionMode = ResolutionMode.OfflineCache,
                    Diagnostics = stale.Diagnostics.Concat([$"Returned stale cache after network failure: {ex.Message}"]).ToArray()
                };
            }

            return BuildError(query, ResolutionStatus.NetworkError, $"Network error: {ex.Message}");
        }
        catch (JsonException ex)
        {
            return BuildError(query, ResolutionStatus.Error, $"Microsoft Learn returned an unexpected search response: {ex.Message}");
        }
    }

    private async Task<DocumentationResult?> TryCanonicalUrlAsync(SymbolQuery query, DocumentationOptions options, CancellationToken cancellationToken)
    {
        var url = _urlBuilder.BuildCanonicalUrl(query, options.View);
        if (url is null)
        {
            return null;
        }

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var record = _pageParser.Parse(url, html, options.IncludeOverloads);

        return new DocumentationResult(
            query.OriginalText,
            query.NormalizedText,
            ResolutionMode.CanonicalUrl,
            ResolutionStatus.Success,
            record,
            Array.Empty<SearchCandidate>(),
            query.Diagnostics);
    }

    private async Task<DocumentationResult> ResolveViaSearchAsync(SymbolQuery query, DocumentationOptions options, CancellationToken cancellationToken)
    {
        var candidates = await SearchAsync(query, cancellationToken);
        if (candidates.Count == 0)
        {
            return new DocumentationResult(
                query.OriginalText,
                query.NormalizedText,
                ResolutionMode.Search,
                ResolutionStatus.NotFound,
                null,
                Array.Empty<SearchCandidate>(),
                query.Diagnostics.Concat(["No official Microsoft Learn matches were found."]).ToArray());
        }

        var topCandidate = candidates[0];
        if (topCandidate.Score < 500 || (candidates.Count > 1 && Math.Abs(topCandidate.Score - candidates[1].Score) < 100))
        {
            return new DocumentationResult(
                query.OriginalText,
                query.NormalizedText,
                ResolutionMode.Search,
                ResolutionStatus.Ambiguous,
                null,
                candidates.Take(8).ToArray(),
                query.Diagnostics.Concat(["Multiple candidates are plausible; refine the query or use --search."]).ToArray());
        }

        using var response = await _httpClient.GetAsync(topCandidate.Url, cancellationToken);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(cancellationToken);
        var record = _pageParser.Parse(topCandidate.Url, html, options.IncludeOverloads);

        return new DocumentationResult(
            query.OriginalText,
            query.NormalizedText,
            ResolutionMode.Search,
            ResolutionStatus.Success,
            record,
            candidates.Take(5).ToArray(),
            query.Diagnostics);
    }

    private async Task<IReadOnlyList<SearchCandidate>> SearchAsync(SymbolQuery query, CancellationToken cancellationToken)
    {
        var url = $"https://learn.microsoft.com/api/search?search={Uri.EscapeDataString(query.SearchText)}&locale=en-us&$top=10";
        var response = await _httpClient.GetFromJsonAsync<LearnSearchResponse>(url, cancellationToken);
        var items = response?.Results ?? [];

        var candidates = items
            .Where(item =>
            {
                var bestUrl = GetBestUrl(item);
                return !string.IsNullOrWhiteSpace(bestUrl) && bestUrl.Contains("/dotnet/api/", StringComparison.OrdinalIgnoreCase);
            })
            .Select(item => new SearchCandidate(
                BuildSymbol(item),
                InferKind(item),
                InferNamespace(item),
                item.Description,
                NormalizeUrl(GetBestUrl(item)!),
                0,
                "learn-search"))
            .ToArray();

        return _ranker.Rank(query, candidates);
    }

    private static string NormalizeUrl(string url)
        => url.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? url : $"https://learn.microsoft.com{url}";

    private static string? GetBestUrl(LearnSearchItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.Url))
        {
            return item.Url;
        }

        if (item.DisplayUrl is JsonElement displayUrl && displayUrl.ValueKind == JsonValueKind.String)
        {
            return displayUrl.GetString();
        }

        if (item.DisplayUrl is JsonElement objectUrl && objectUrl.ValueKind == JsonValueKind.Object)
        {
            if (objectUrl.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
            {
                return content.GetString();
            }

            if (objectUrl.TryGetProperty("href", out var href) && href.ValueKind == JsonValueKind.String)
            {
                return href.GetString();
            }
        }

        return null;
    }

    private static string BuildSymbol(LearnSearchItem item)
    {
        var title = item.Title?.Trim() ?? GetBestUrl(item)?.Split('/').LastOrDefault() ?? "Unknown";
        return title.Replace(" Method", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" Class", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" Struct", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" Interface", string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static SymbolKind InferKind(LearnSearchItem item)
    {
        var title = item.Title ?? string.Empty;
        if (title.Contains(" Method", StringComparison.OrdinalIgnoreCase))
        {
            return SymbolKind.Method;
        }

        if (title.Contains(" Property", StringComparison.OrdinalIgnoreCase))
        {
            return SymbolKind.Property;
        }

        if (title.Contains(" Field", StringComparison.OrdinalIgnoreCase))
        {
            return SymbolKind.Field;
        }

        if (title.Contains(" Event", StringComparison.OrdinalIgnoreCase))
        {
            return SymbolKind.Event;
        }

        return SymbolKind.Type;
    }

    private static string? InferNamespace(LearnSearchItem item)
    {
        var url = GetBestUrl(item);
        if (url is null)
        {
            return null;
        }

        var segment = url.Split("/dotnet/api/", StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
        if (string.IsNullOrWhiteSpace(segment))
        {
            return null;
        }

        var pieces = segment.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return pieces.Length > 1 ? string.Join('.', pieces.Take(pieces.Length - 1)) : null;
    }

    private static DocumentationResult BuildError(SymbolQuery query, ResolutionStatus status, string message)
        => new(
            query.OriginalText,
            query.NormalizedText,
            ResolutionMode.None,
            status,
            null,
            Array.Empty<SearchCandidate>(),
            query.Diagnostics.Concat([message]).ToArray());
}
