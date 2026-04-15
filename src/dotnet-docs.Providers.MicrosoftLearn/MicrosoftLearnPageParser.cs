using System.Net;
using System.Text.RegularExpressions;
using DotNetDocs.Core;

namespace DotNetDocs.Providers.MicrosoftLearn;

public sealed partial class MicrosoftLearnPageParser
{
    [GeneratedRegex("<meta\\s+name=\"description\"\\s+content=\"(?<value>[^\"]+)\"", RegexOptions.IgnoreCase)]
    private static partial Regex DescriptionRegex();

    [GeneratedRegex("<h1[^>]*>(?<value>.*?)</h1>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex H1Regex();

    [GeneratedRegex("<title>(?<value>.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TitleRegex();

    public DocumentationRecord Parse(string url, string html, bool includeOverloads)
    {
        var text = ExtractText(html);
        var lines = text
            .Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(line => !line.StartsWith("Skip to main content", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        var symbol = Decode(H1Regex().Match(html).Groups["value"].Value);
        if (string.IsNullOrWhiteSpace(symbol))
        {
            symbol = Decode(TitleRegex().Match(html).Groups["value"].Value).Split('|')[0].Trim();
        }

        var summary = Decode(DescriptionRegex().Match(html).Groups["value"].Value);
        var kind = InferKind(symbol, lines);
        var namespaceName = FindDefinitionValue(lines, "Namespace");
        var assemblyName = FindDefinitionValue(lines, "Assembly");
        var overloads = includeOverloads ? ParseOverloads(lines, symbol) : Array.Empty<SearchCandidate>();
        var signatures = ParseSignatures(lines, kind, symbol, includeOverloads ? overloads : Array.Empty<SearchCandidate>());

        return new DocumentationRecord(
            symbol,
            kind,
            namespaceName,
            summary,
            signatures,
            overloads.Count > 0 ? overloads.Count : null,
            url,
            assemblyName,
            overloads,
            Array.Empty<string>());
    }

    private static string ExtractText(string html)
    {
        var value = Regex.Replace(html, "<script.*?</script>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        value = Regex.Replace(value, "<style.*?</style>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        value = Regex.Replace(value, "</?(h1|h2|h3|h4|p|div|section|article|li|ul|ol|dt|dd|tr|table|pre|code)[^>]*>", "\n", RegexOptions.IgnoreCase);
        value = Regex.Replace(value, "<br\\s*/?>", "\n", RegexOptions.IgnoreCase);
        value = Regex.Replace(value, "<[^>]+>", string.Empty);
        value = WebUtility.HtmlDecode(value);
        value = Regex.Replace(value, @"\r", string.Empty);
        value = Regex.Replace(value, @"\n{2,}", "\n");
        return value;
    }

    private static string Decode(string value) => WebUtility.HtmlDecode(value).Trim();

    private static SymbolKind InferKind(string symbol, IReadOnlyList<string> lines)
    {
        if (symbol.Contains('('))
        {
            return SymbolKind.Method;
        }

        var lowerLines = string.Join('\n', lines).ToLowerInvariant();
        if (lowerLines.Contains("method"))
        {
            return SymbolKind.Method;
        }

        if (lowerLines.Contains("property"))
        {
            return SymbolKind.Property;
        }

        if (lowerLines.Contains("field"))
        {
            return SymbolKind.Field;
        }

        if (lowerLines.Contains("event"))
        {
            return SymbolKind.Event;
        }

        return SymbolKind.Type;
    }

    private static string? FindDefinitionValue(IReadOnlyList<string> lines, string label)
    {
        for (var i = 0; i < lines.Count - 1; i++)
        {
            if (string.Equals(lines[i], label, StringComparison.OrdinalIgnoreCase))
            {
                return lines[i + 1];
            }
        }

        return null;
    }

    private static IReadOnlyList<string> ParseSignatures(
        IReadOnlyList<string> lines,
        SymbolKind kind,
        string symbol,
        IReadOnlyList<SearchCandidate> overloads)
    {
        var signatures = new List<string>();
        foreach (var line in lines)
        {
            if (line.Contains("public ", StringComparison.Ordinal) ||
                line.Contains("static ", StringComparison.Ordinal) ||
                line.Contains("abstract ", StringComparison.Ordinal) ||
                line.Contains("virtual ", StringComparison.Ordinal))
            {
                signatures.Add(line.Trim());
            }
        }

        if (signatures.Count > 0)
        {
            return signatures.Distinct(StringComparer.Ordinal).Take(5).ToArray();
        }

        if (kind == SymbolKind.Method && overloads.Count > 0)
        {
            return overloads.Select(overload => overload.Symbol).Distinct(StringComparer.OrdinalIgnoreCase).Take(5).ToArray();
        }

        return string.IsNullOrWhiteSpace(symbol) ? Array.Empty<string>() : [symbol];
    }

    private static IReadOnlyList<SearchCandidate> ParseOverloads(IReadOnlyList<string> lines, string symbol)
    {
        var methodBase = symbol.Split('(')[0].Trim();
        if (string.IsNullOrWhiteSpace(methodBase))
        {
            return Array.Empty<SearchCandidate>();
        }

        var overloads = new List<SearchCandidate>();
        foreach (var line in lines)
        {
            if (!line.StartsWith(methodBase, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!line.Contains('('))
            {
                continue;
            }

            overloads.Add(new SearchCandidate(line.Trim(), SymbolKind.Method, null, null, string.Empty, 0, "page"));
        }

        return overloads
            .DistinctBy(candidate => candidate.Symbol, StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToArray();
    }
}
