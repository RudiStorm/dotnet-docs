using DotNetDocs.Core;

namespace DotNetDocs.Tests;

public sealed class CandidateRankerTests
{
    [Fact]
    public void Rank_PrefersExactFullyQualifiedSymbol()
    {
        var query = new SymbolQueryNormalizer().Normalize("string.join");
        var candidates = new[]
        {
            new SearchCandidate("System.IO.Path.Join", SymbolKind.Method, "System.IO", null, "https://learn.microsoft.com/en-us/dotnet/api/system.io.path.join", 0, "test"),
            new SearchCandidate("System.String.Join", SymbolKind.Method, "System", null, "https://learn.microsoft.com/en-us/dotnet/api/system.string.join", 0, "test")
        };

        var ranked = new CandidateRanker().Rank(query, candidates);

        Assert.Equal("System.String.Join", ranked[0].Symbol);
        Assert.True(ranked[0].Score > ranked[1].Score);
    }
}
