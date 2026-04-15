namespace DotNetDocs.Core;

public sealed class CandidateRanker
{
    public IReadOnlyList<SearchCandidate> Rank(SymbolQuery query, IEnumerable<SearchCandidate> candidates)
    {
        var normalized = query.NormalizedText;
        var simple = query.MemberName is null ? query.TypeName : $"{query.TypeName}.{query.MemberName}";

        return candidates
            .Select(candidate => candidate with { Score = Score(query, candidate, normalized, simple) })
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static double Score(SymbolQuery query, SearchCandidate candidate, string normalized, string simple)
    {
        var score = candidate.Score;
        if (candidate.Symbol.Equals(normalized, StringComparison.OrdinalIgnoreCase))
        {
            score += 1000;
        }

        if (candidate.Symbol.Equals(simple, StringComparison.OrdinalIgnoreCase))
        {
            score += 800;
        }

        if (query.CandidateSymbols.Any(value => candidate.Symbol.Equals(value, StringComparison.OrdinalIgnoreCase)))
        {
            score += 600;
        }

        if (!string.IsNullOrWhiteSpace(query.NamespaceName) &&
            candidate.Namespace?.StartsWith(query.NamespaceName, StringComparison.OrdinalIgnoreCase) == true)
        {
            score += 200;
        }

        if (candidate.Url.Contains("/dotnet/api/", StringComparison.OrdinalIgnoreCase))
        {
            score += 100;
        }

        if (candidate.Symbol.Contains(query.TypeName, StringComparison.OrdinalIgnoreCase))
        {
            score += 75;
        }

        if (query.MemberName is not null &&
            candidate.Symbol.Contains(query.MemberName, StringComparison.OrdinalIgnoreCase))
        {
            score += 75;
        }

        return score;
    }
}
