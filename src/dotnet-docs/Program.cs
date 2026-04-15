using System.CommandLine;
using System.Diagnostics;
using DotNetDocs.Core;
using DotNetDocs.Providers.MicrosoftLearn;
using DotNetDocs.Rendering;

namespace DotNetDocs.Cli;

internal static class Program
{
    private const int SuccessExitCode = 0;
    private const int LookupFailureExitCode = 1;
    private const int InvalidInputExitCode = 2;
    private const int NetworkFailureExitCode = 3;

    private static async Task<int> Main(string[] args)
    {
        var normalizer = new SymbolQueryNormalizer();
        var cacheStore = new FileCacheStore();
        var provider = new MicrosoftLearnProvider(
            new HttpClient(new SocketsHttpHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.All
            })
            {
                Timeout = TimeSpan.FromSeconds(15)
            },
            cacheStore,
            new CandidateRanker(),
            new MicrosoftLearnUrlBuilder(),
            new MicrosoftLearnPageParser());
        var renderer = new DocumentationRenderer();

        var root = new RootCommand(".NET API docs from the terminal, backed by official Microsoft Learn content.");
        ConfigureDocsCommand(root, normalizer, provider, renderer);

        var docsCommand = new Command("docs", "Resolve official .NET API documentation for a type or member.");
        ConfigureDocsCommand(docsCommand, normalizer, provider, renderer);
        root.Subcommands.Add(docsCommand);

        var cacheCommand = new Command("cache", "Inspect or clear the local metadata cache.");
        var cachePathCommand = new Command("path", "Print the cache directory path.");
        cachePathCommand.SetAction(parseResult =>
        {
            Console.WriteLine(cacheStore.GetCacheDirectory());
            return SuccessExitCode;
        });

        var cacheClearCommand = new Command("clear", "Delete cached documentation metadata.");
        cacheClearCommand.SetAction(async (parseResult, cancellationToken) =>
        {
            await cacheStore.ClearAsync(cancellationToken);
            Console.WriteLine("Cache cleared.");
            return SuccessExitCode;
        });

        cacheCommand.Subcommands.Add(cachePathCommand);
        cacheCommand.Subcommands.Add(cacheClearCommand);
        root.Subcommands.Add(cacheCommand);

        return await root.Parse(args).InvokeAsync();
    }

    private static void ConfigureDocsCommand(
        Command command,
        ISymbolQueryNormalizer normalizer,
        IDocumentationProvider provider,
        DocumentationRenderer renderer)
    {
        var queryArgument = new Argument<string?>("query")
        {
            Description = "Type or member name to resolve against official .NET API docs."
        };

        var openOption = new Option<bool>("--open")
        {
            Description = "Open the resolved Microsoft Learn page in the system browser."
        };
        var urlOption = new Option<bool>("--url")
        {
            Description = "Print only the resolved documentation URL."
        };
        var jsonOption = new Option<bool>("--json")
        {
            Description = "Emit structured JSON for scripts and editor integrations."
        };
        var searchOption = new Option<bool>("--search")
        {
            Description = "Force official search mode instead of canonical URL resolution."
        };
        var overloadsOption = new Option<bool>("--overloads")
        {
            Description = "List overloads for method results when available."
        };
        var noCacheOption = new Option<bool>("--no-cache")
        {
            Description = "Bypass the local cache for this lookup."
        };
        var cacheTtlOption = new Option<int>("--cache-ttl-hours")
        {
            Description = "Cache TTL in hours.",
            DefaultValueFactory = _ => 24
        };
        var viewOption = new Option<string>("--view")
        {
            Description = "Microsoft Learn API view, for example net-10.0.",
            DefaultValueFactory = _ => "net-10.0"
        };
        var verboseOption = new Option<bool>("--verbose")
        {
            Description = "Include resolver diagnostics."
        };

        command.Arguments.Add(queryArgument);
        command.Options.Add(openOption);
        command.Options.Add(urlOption);
        command.Options.Add(jsonOption);
        command.Options.Add(searchOption);
        command.Options.Add(overloadsOption);
        command.Options.Add(noCacheOption);
        command.Options.Add(cacheTtlOption);
        command.Options.Add(viewOption);
        command.Options.Add(verboseOption);

        command.SetAction((parseResult, cancellationToken) =>
        {
            var queryText = parseResult.GetValue(queryArgument);
            if (string.IsNullOrWhiteSpace(queryText))
            {
                Console.Error.WriteLine("A symbol query is required.");
                return Task.FromResult(InvalidInputExitCode);
            }

            return RunDocsAsync(
                queryText,
                parseResult.GetValue(openOption),
                parseResult.GetValue(urlOption),
                parseResult.GetValue(jsonOption),
                parseResult.GetValue(searchOption),
                parseResult.GetValue(overloadsOption),
                parseResult.GetValue(noCacheOption),
                parseResult.GetValue(cacheTtlOption),
                parseResult.GetValue(viewOption) ?? "net-10.0",
                parseResult.GetValue(verboseOption),
                normalizer,
                provider,
                renderer,
                cancellationToken);
        });
    }

    private static async Task<int> RunDocsAsync(
        string queryText,
        bool openInBrowser,
        bool urlOnly,
        bool json,
        bool forceSearch,
        bool includeOverloads,
        bool disableCache,
        int cacheTtlHours,
        string view,
        bool verbose,
        ISymbolQueryNormalizer normalizer,
        IDocumentationProvider provider,
        DocumentationRenderer renderer,
        CancellationToken cancellationToken)
    {
        var query = normalizer.Normalize(queryText);
        if (query.Diagnostics.Contains("Query is empty.", StringComparer.Ordinal))
        {
            Console.Error.WriteLine("A symbol query is required.");
            return InvalidInputExitCode;
        }

        var options = new DocumentationOptions(
            forceSearch,
            includeOverloads,
            disableCache,
            TimeSpan.FromHours(Math.Max(cacheTtlHours, 1)),
            view,
            verbose);

        var result = await provider.ResolveAsync(query, options, cancellationToken);
        var output = json ? renderer.RenderJson(result) : renderer.RenderTerminal(result, urlOnly);
        Console.WriteLine(output);

        if (openInBrowser && result.Record is not null)
        {
            OpenUrl(result.Record.Url);
        }

        return result.Status switch
        {
            ResolutionStatus.Success => SuccessExitCode,
            ResolutionStatus.Invalid => InvalidInputExitCode,
            ResolutionStatus.NetworkError => NetworkFailureExitCode,
            _ => LookupFailureExitCode
        };
    }

    private static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }
}
