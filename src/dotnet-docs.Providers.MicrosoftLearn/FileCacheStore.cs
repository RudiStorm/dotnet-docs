using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DotNetDocs.Core;

namespace DotNetDocs.Providers.MicrosoftLearn;

public sealed class FileCacheStore : ICacheStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _cacheDirectory;

    public FileCacheStore(string? cacheDirectory = null)
    {
        _cacheDirectory = cacheDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "dotnet-docs",
            "cache");

        Directory.CreateDirectory(_cacheDirectory);
    }

    public async Task<T?> GetAsync<T>(string key, TimeSpan ttl, CancellationToken cancellationToken) where T : class
    {
        var entry = await ReadEntryAsync<T>(key, cancellationToken);
        if (entry is null)
        {
            return null;
        }

        if (DateTimeOffset.UtcNow - entry.StoredAt > ttl)
        {
            return null;
        }

        return entry.Value;
    }

    public async Task<T?> GetStaleAsync<T>(string key, CancellationToken cancellationToken) where T : class
    {
        var entry = await ReadEntryAsync<T>(key, cancellationToken);
        return entry?.Value;
    }

    public async Task SetAsync<T>(string key, T value, CancellationToken cancellationToken) where T : class
    {
        var path = GetPath(key);
        await using var stream = File.Create(path);
        var entry = new CacheEntry<T>(DateTimeOffset.UtcNow, value);
        await JsonSerializer.SerializeAsync(stream, entry, JsonOptions, cancellationToken);
    }

    public Task ClearAsync(CancellationToken cancellationToken)
    {
        foreach (var file in Directory.EnumerateFiles(_cacheDirectory, "*.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            File.Delete(file);
        }

        return Task.CompletedTask;
    }

    public string GetCacheDirectory() => _cacheDirectory;

    private async Task<CacheEntry<T>?> ReadEntryAsync<T>(string key, CancellationToken cancellationToken) where T : class
    {
        var path = GetPath(key);
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<CacheEntry<T>>(stream, JsonOptions, cancellationToken);
    }

    private string GetPath(string key)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)));
        return Path.Combine(_cacheDirectory, $"{hash}.json");
    }

    private sealed record CacheEntry<T>(DateTimeOffset StoredAt, T Value);
}
