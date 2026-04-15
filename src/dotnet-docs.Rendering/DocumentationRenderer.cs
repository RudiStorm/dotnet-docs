using System.Text;
using System.Text.Json;
using DotNetDocs.Core;

namespace DotNetDocs.Rendering;

public sealed class DocumentationRenderer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public string RenderTerminal(DocumentationResult result, bool urlOnly)
    {
        if (urlOnly)
        {
            return result.Record?.Url ?? string.Empty;
        }

        var builder = new StringBuilder();
        if (result.IsSuccess)
        {
            var record = result.Record!;
            builder.AppendLine(record.Symbol);
            builder.AppendLine($"Kind: {record.Kind}");
            if (!string.IsNullOrWhiteSpace(record.Namespace))
            {
                builder.AppendLine($"Namespace: {record.Namespace}");
            }

            if (!string.IsNullOrWhiteSpace(record.AssemblyName))
            {
                builder.AppendLine($"Assembly: {record.AssemblyName}");
            }

            builder.AppendLine();
            if (!string.IsNullOrWhiteSpace(record.Summary))
            {
                builder.AppendLine(record.Summary);
                builder.AppendLine();
            }

            if (record.OverloadCount is > 0)
            {
                builder.AppendLine($"Overloads: {record.OverloadCount}");
                builder.AppendLine();
            }

            if (record.Signatures.Count > 0)
            {
                builder.AppendLine("Signatures:");
                foreach (var signature in record.Signatures)
                {
                    builder.AppendLine($"- {signature}");
                }

                builder.AppendLine();
            }

            if (record.Overloads.Count > 0)
            {
                builder.AppendLine("Overload list:");
                foreach (var overload in record.Overloads.Take(10))
                {
                    builder.AppendLine($"- {overload.Symbol}");
                }

                builder.AppendLine();
            }

            builder.AppendLine("URL:");
            builder.AppendLine(record.Url);
        }
        else
        {
            builder.AppendLine(result.Status switch
            {
                ResolutionStatus.Ambiguous => "Multiple matches found:",
                ResolutionStatus.NotFound => "No official Microsoft Learn match was found.",
                ResolutionStatus.Invalid => "The query is invalid.",
                ResolutionStatus.NetworkError => "The request failed before docs could be resolved.",
                _ => "The docs lookup did not succeed."
            });

            if (result.Candidates.Count > 0)
            {
                builder.AppendLine();
                for (var i = 0; i < result.Candidates.Count; i++)
                {
                    var candidate = result.Candidates[i];
                    builder.AppendLine($"{i + 1}. {candidate.Symbol} ({candidate.Kind})");
                    if (!string.IsNullOrWhiteSpace(candidate.Namespace))
                    {
                        builder.AppendLine($"   Namespace: {candidate.Namespace}");
                    }

                    if (!string.IsNullOrWhiteSpace(candidate.Summary))
                    {
                        builder.AppendLine($"   {candidate.Summary}");
                    }

                    builder.AppendLine($"   {candidate.Url}");
                }
            }
        }

        if (result.Diagnostics.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Diagnostics:");
            foreach (var diagnostic in result.Diagnostics)
            {
                builder.AppendLine($"- {diagnostic}");
            }
        }

        return builder.ToString().TrimEnd();
    }

    public string RenderJson(DocumentationResult result)
    {
        var payload = new JsonOutputModel(
            result.Query,
            result.NormalizedQuery,
            result.ResolutionMode.ToString(),
            result.Status.ToString(),
            result.Record?.Symbol,
            result.Record?.Kind.ToString(),
            result.Record?.Namespace,
            result.Record?.Signatures ?? Array.Empty<string>(),
            result.Record?.Summary,
            result.Record?.Url,
            result.Record?.AssemblyName,
            result.Record?.OverloadCount,
            result.Candidates,
            result.Diagnostics);

        return JsonSerializer.Serialize(payload, JsonOptions);
    }
}
