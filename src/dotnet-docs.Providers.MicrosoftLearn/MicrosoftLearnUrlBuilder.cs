using DotNetDocs.Core;

namespace DotNetDocs.Providers.MicrosoftLearn;

public sealed class MicrosoftLearnUrlBuilder
{
    private readonly string _locale;

    public MicrosoftLearnUrlBuilder(string locale = "en-us")
    {
        _locale = locale;
    }

    public string? BuildCanonicalUrl(SymbolQuery query, string view)
    {
        if (!query.IsFullyQualified)
        {
            return null;
        }

        var slug = query.NormalizedText.ToLowerInvariant();
        return $"https://learn.microsoft.com/{_locale}/dotnet/api/{slug}?view={view}";
    }
}
