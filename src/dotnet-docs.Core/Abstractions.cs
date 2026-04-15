namespace DotNetDocs.Core;

public interface ISymbolQueryNormalizer
{
    SymbolQuery Normalize(string input);
}

public interface IDocumentationProvider
{
    Task<DocumentationResult> ResolveAsync(SymbolQuery query, DocumentationOptions options, CancellationToken cancellationToken);
}

public interface ICacheStore
{
    Task<T?> GetAsync<T>(string key, TimeSpan ttl, CancellationToken cancellationToken) where T : class;
    Task<T?> GetStaleAsync<T>(string key, CancellationToken cancellationToken) where T : class;
    Task SetAsync<T>(string key, T value, CancellationToken cancellationToken) where T : class;
    Task ClearAsync(CancellationToken cancellationToken);
    string GetCacheDirectory();
}
