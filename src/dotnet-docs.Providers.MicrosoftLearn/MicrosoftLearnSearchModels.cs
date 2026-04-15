using System.Text.Json;
using System.Text.Json.Serialization;

namespace DotNetDocs.Providers.MicrosoftLearn;

internal sealed class LearnSearchResponse
{
    [JsonPropertyName("results")]
    public List<LearnSearchItem>? Results { get; init; }
}

internal sealed class LearnSearchItem
{
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("displayUrl")]
    public JsonElement? DisplayUrl { get; init; }

    [JsonPropertyName("category")]
    public string? Category { get; init; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtraProperties { get; init; }
}
