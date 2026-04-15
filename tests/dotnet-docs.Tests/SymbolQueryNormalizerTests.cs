using DotNetDocs.Core;

namespace DotNetDocs.Tests;

public sealed class SymbolQueryNormalizerTests
{
    private readonly SymbolQueryNormalizer _normalizer = new();

    [Theory]
    [InlineData("string.join", "System.String.Join")]
    [InlineData("String.Join", "System.String.Join")]
    [InlineData("System.String.Join", "System.String.Join")]
    [InlineData("Console.WriteLine", "System.Console.WriteLine")]
    [InlineData("List<T>", "System.Collections.Generic.List-1")]
    [InlineData("Dictionary<TKey,TValue>.TryGetValue", "System.Collections.Generic.Dictionary-2.TryGetValue")]
    public void Normalize_MapsExpectedSymbolShapes(string input, string expected)
    {
        var result = _normalizer.Normalize(input);
        Assert.Equal(expected, result.NormalizedText);
    }

    [Fact]
    public void Normalize_DetectsShortUnqualifiedQueries()
    {
        var result = _normalizer.Normalize("Join");
        Assert.Contains(result.Diagnostics, message => message.Contains("not fully qualified", StringComparison.OrdinalIgnoreCase));
    }
}
