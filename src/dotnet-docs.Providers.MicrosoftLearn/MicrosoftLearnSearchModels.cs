namespace DotNetDocs.Providers.MicrosoftLearn;

internal sealed record LearnSearchResponse(List<LearnSearchItem>? Results);

internal sealed record LearnSearchItem(
    string? Title,
    string? Url,
    string? Description,
    string? DisplayUrl,
    string? Category);
