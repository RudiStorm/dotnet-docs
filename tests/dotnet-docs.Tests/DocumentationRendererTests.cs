using DotNetDocs.Core;
using DotNetDocs.Rendering;

namespace DotNetDocs.Tests;

public sealed class DocumentationRendererTests
{
    [Fact]
    public void RenderTerminal_PrintsCompactExactMatch()
    {
        var result = new DocumentationResult(
            "string.join",
            "System.String.Join",
            ResolutionMode.CanonicalUrl,
            ResolutionStatus.Success,
            new DocumentationRecord(
                "System.String.Join",
                SymbolKind.Method,
                "System",
                "Concatenates the elements of a string array.",
                ["public static string Join(string? separator, params string?[] value);"],
                12,
                "https://learn.microsoft.com/en-us/dotnet/api/system.string.join?view=net-10.0",
                "System.Runtime.dll",
                Array.Empty<SearchCandidate>(),
                Array.Empty<string>()),
            Array.Empty<SearchCandidate>(),
            Array.Empty<string>());

        var output = new DocumentationRenderer().RenderTerminal(result, urlOnly: false);

        Assert.Contains("System.String.Join", output, StringComparison.Ordinal);
        Assert.Contains("Overloads: 12", output, StringComparison.Ordinal);
        Assert.Contains("https://learn.microsoft.com", output, StringComparison.Ordinal);
    }
}
