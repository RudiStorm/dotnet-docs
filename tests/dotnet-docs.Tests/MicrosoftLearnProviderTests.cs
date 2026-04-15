using System.Net;
using System.Net.Http;
using DotNetDocs.Core;
using DotNetDocs.Providers.MicrosoftLearn;

namespace DotNetDocs.Tests;

public sealed class MicrosoftLearnProviderTests
{
    [Fact]
    public async Task ResolveAsync_UsesCanonicalUrlWhenAvailable()
    {
        var handler = new StubHttpMessageHandler(request =>
        {
            if (request.RequestUri!.AbsoluteUri.Contains("/dotnet/api/system.string.join", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                        <html>
                        <head>
                          <title>System.String.Join Method | Microsoft Learn</title>
                          <meta name="description" content="Concatenates strings." />
                        </head>
                        <body>
                          <h1>System.String.Join</h1>
                          <div>Namespace</div><div>System</div>
                          <div>Assembly</div><div>System.Runtime.dll</div>
                          <pre>public static string Join(string? separator, params string?[] value);</pre>
                        </body>
                        </html>
                        """)
                };
            }

            throw new InvalidOperationException($"Unexpected URL: {request.RequestUri}");
        });

        var provider = CreateProvider(handler);
        var query = new SymbolQueryNormalizer().Normalize("string.join");
        var result = await provider.ResolveAsync(
            query,
            new DocumentationOptions(false, true, true, TimeSpan.FromHours(1), "net-10.0"),
            CancellationToken.None);

        Assert.Equal(ResolutionStatus.Success, result.Status);
        Assert.Equal("System.String.Join", result.Record?.Symbol);
        Assert.Equal("System", result.Record?.Namespace);
    }

    private static MicrosoftLearnProvider CreateProvider(HttpMessageHandler handler)
        => new(
            new HttpClient(handler),
            new FileCacheStore(Path.Combine(Path.GetTempPath(), "dotnet-docs-tests", Guid.NewGuid().ToString("N"))),
            new CandidateRanker(),
            new MicrosoftLearnUrlBuilder(),
            new MicrosoftLearnPageParser());

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_responder(request));
    }
}
