using System.Text;

namespace DotNetDocs.Core;

public sealed class SymbolQueryNormalizer : ISymbolQueryNormalizer
{
    private static readonly Dictionary<string, string> AliasMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["bool"] = "System.Boolean",
        ["byte"] = "System.Byte",
        ["char"] = "System.Char",
        ["decimal"] = "System.Decimal",
        ["double"] = "System.Double",
        ["float"] = "System.Single",
        ["int"] = "System.Int32",
        ["long"] = "System.Int64",
        ["nint"] = "System.IntPtr",
        ["nuint"] = "System.UIntPtr",
        ["object"] = "System.Object",
        ["sbyte"] = "System.SByte",
        ["short"] = "System.Int16",
        ["string"] = "System.String",
        ["uint"] = "System.UInt32",
        ["ulong"] = "System.UInt64",
        ["ushort"] = "System.UInt16",
        ["void"] = "System.Void",
        ["console"] = "System.Console",
        ["enumerable"] = "System.Linq.Enumerable",
        ["list"] = "System.Collections.Generic.List",
        ["dictionary"] = "System.Collections.Generic.Dictionary"
    };

    public SymbolQuery Normalize(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return new SymbolQuery(
                input,
                string.Empty,
                string.Empty,
                null,
                string.Empty,
                null,
                0,
                false,
                false,
                Array.Empty<string>(),
                ["Query is empty."]);
        }

        var trimmed = input.Trim();
        var diagnostics = new List<string>();
        var splitIndex = FindMemberSeparator(trimmed);
        var typePart = splitIndex >= 0 ? trimmed[..splitIndex] : trimmed;
        var memberPart = splitIndex >= 0 ? trimmed[(splitIndex + 1)..] : null;

        var aliasExpanded = false;
        var expandedType = ExpandTypeAlias(typePart, ref aliasExpanded, diagnostics);
        var genericArity = CountGenericArity(expandedType);
        var normalizedType = NormalizeTypeSyntax(expandedType, genericArity);
        var normalizedMember = NormalizeMemberSyntax(memberPart);
        var normalizedText = normalizedMember is null ? normalizedType : $"{normalizedType}.{normalizedMember}";
        var namespaceName = GetNamespace(normalizedType);
        var typeName = GetTypeName(normalizedType);
        var isFullyQualified = normalizedType.Contains('.', StringComparison.Ordinal);

        var candidates = BuildCandidates(normalizedType, normalizedMember, typeName, genericArity);
        var searchText = normalizedMember is null ? normalizedType : $"{normalizedType}.{normalizedMember}";

        return new SymbolQuery(
            trimmed,
            normalizedText,
            searchText,
            namespaceName,
            typeName,
            normalizedMember,
            genericArity,
            aliasExpanded,
            isFullyQualified,
            candidates,
            diagnostics);
    }

    private static int FindMemberSeparator(string input)
    {
        var depth = 0;
        for (var i = input.Length - 1; i >= 0; i--)
        {
            var ch = input[i];
            if (ch == '>')
            {
                depth++;
            }
            else if (ch == '<')
            {
                depth--;
            }
            else if (ch == '.' && depth == 0)
            {
                return i;
            }
        }

        return -1;
    }

    private static string ExpandTypeAlias(string typePart, ref bool aliasExpanded, List<string> diagnostics)
    {
        var genericStart = typePart.IndexOf('<');
        var baseName = genericStart >= 0 ? typePart[..genericStart] : typePart;
        var suffix = genericStart >= 0 ? typePart[genericStart..] : string.Empty;

        if (AliasMap.TryGetValue(baseName, out var expanded))
        {
            aliasExpanded = true;
            return expanded + suffix;
        }

        if (!baseName.Contains('.', StringComparison.Ordinal) && char.IsUpper(baseName[0]))
        {
            if (AliasMap.TryGetValue(baseName.ToLowerInvariant(), out expanded))
            {
                aliasExpanded = true;
                return expanded + suffix;
            }

            diagnostics.Add("Query is not fully qualified; search fallback may be needed.");
        }

        return typePart;
    }

    private static int CountGenericArity(string typeName)
    {
        var start = typeName.IndexOf('<');
        if (start < 0)
        {
            return 0;
        }

        var end = typeName.LastIndexOf('>');
        if (end <= start)
        {
            return 0;
        }

        var argumentList = typeName[(start + 1)..end];
        if (string.IsNullOrWhiteSpace(argumentList))
        {
            return 0;
        }

        return argumentList.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private static string NormalizeTypeSyntax(string typeName, int genericArity)
    {
        var sb = new StringBuilder(typeName.Length);
        var insideGeneric = false;
        foreach (var ch in typeName)
        {
            if (ch == '<')
            {
                insideGeneric = true;
                if (genericArity > 0)
                {
                    sb.Append('-').Append(genericArity);
                }

                continue;
            }

            if (ch == '>')
            {
                insideGeneric = false;
                continue;
            }

            if (!insideGeneric && !char.IsWhiteSpace(ch))
            {
                sb.Append(ch);
            }
        }

        return sb.ToString();
    }

    private static string? NormalizeMemberSyntax(string? memberName)
    {
        if (string.IsNullOrWhiteSpace(memberName))
        {
            return null;
        }

        var trimmed = memberName.Trim();
        var parenIndex = trimmed.IndexOf('(');
        trimmed = parenIndex >= 0 ? trimmed[..parenIndex] : trimmed;

        if (trimmed.All(ch => !char.IsLetter(ch) || char.IsLower(ch)))
        {
            return char.ToUpperInvariant(trimmed[0]) + trimmed[1..];
        }

        return trimmed;
    }

    private static string? GetNamespace(string normalizedType)
    {
        var lastDot = normalizedType.LastIndexOf('.');
        return lastDot < 0 ? null : normalizedType[..lastDot];
    }

    private static string GetTypeName(string normalizedType)
    {
        var lastDot = normalizedType.LastIndexOf('.');
        return lastDot < 0 ? normalizedType : normalizedType[(lastDot + 1)..];
    }

    private static IReadOnlyList<string> BuildCandidates(string normalizedType, string? normalizedMember, string typeName, int genericArity)
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            normalizedMember is null ? normalizedType : $"{normalizedType}.{normalizedMember}"
        };

        if (!normalizedType.Contains('.', StringComparison.Ordinal))
        {
            candidates.Add(normalizedMember is null ? typeName : $"{typeName}.{normalizedMember}");
        }

        if (genericArity > 0)
        {
            var nongenericType = normalizedType[..normalizedType.LastIndexOf('-')];
            candidates.Add(normalizedMember is null ? nongenericType : $"{nongenericType}.{normalizedMember}");
        }

        return candidates.ToArray();
    }
}
