using System.Collections.Immutable;

namespace Lattice.Core.Generation;

internal static class SemanticCollectionInputParser
{
    public static bool TryParse(
        string source,
        SemanticCollectionInputFormat format,
        out SemanticCollection? collection)
    {
        collection = null;
        if (string.IsNullOrWhiteSpace(source) ||
            source.Length > SemanticCollectionValidator.MaximumItemValueLength ||
            format is null ||
            string.IsNullOrEmpty(format.OpeningDelimiter) ||
            string.IsNullOrEmpty(format.ClosingDelimiter) ||
            string.IsNullOrEmpty(format.ItemSeparator) ||
            format.ItemFormats.IsDefaultOrEmpty ||
            source.Length < format.OpeningDelimiter.Length + format.ClosingDelimiter.Length ||
            !source.StartsWith(format.OpeningDelimiter, StringComparison.Ordinal) ||
            !source.EndsWith(format.ClosingDelimiter, StringComparison.Ordinal))
        {
            return false;
        }

        var content = source.Substring(
            format.OpeningDelimiter.Length,
            source.Length - format.OpeningDelimiter.Length - format.ClosingDelimiter.Length);
        if (string.IsNullOrWhiteSpace(content))
        {
            collection = new SemanticCollection(ImmutableArray<SemanticCollectionItem>.Empty);
            return true;
        }

        if (!TrySplitItems(content, format, out var sourceItems) ||
            sourceItems.IsDefaultOrEmpty ||
            sourceItems.Length > SemanticCollectionValidator.MaximumItems)
        {
            return false;
        }

        var items = ImmutableArray.CreateBuilder<SemanticCollectionItem>(sourceItems.Length);
        foreach (var sourceItem in sourceItems)
        {
            if (!TryParseItem(sourceItem, format.ItemFormats, out var item))
            {
                return false;
            }

            items.Add(item!);
        }

        var parsed = new SemanticCollection(items.ToImmutable());
        if (!SemanticCollectionValidator.IsValid(parsed))
        {
            return false;
        }

        collection = parsed;
        return true;
    }

    private static bool TrySplitItems(
        string content,
        SemanticCollectionInputFormat format,
        out ImmutableArray<string> items)
    {
        items = ImmutableArray<string>.Empty;
        var builder = ImmutableArray.CreateBuilder<string>();
        SemanticCollectionInputItemFormat? activeFormat = null;
        var itemStart = 0;
        var index = 0;

        while (index < content.Length)
        {
            if (activeFormat is not null)
            {
                if (TryMatchEscape(content, index, activeFormat, out var escapeLength))
                {
                    index += escapeLength;
                    continue;
                }

                if (StartsWith(content, index, activeFormat.ClosingDelimiter))
                {
                    index += activeFormat.ClosingDelimiter.Length;
                    activeFormat = null;
                    continue;
                }

                index++;
                continue;
            }

            var openingFormat = format.ItemFormats.FirstOrDefault(candidate =>
                candidate.OpeningDelimiter.Length > 0 &&
                StartsWith(content, index, candidate.OpeningDelimiter));
            if (openingFormat is not null)
            {
                activeFormat = openingFormat;
                index += openingFormat.OpeningDelimiter.Length;
                continue;
            }

            if (StartsWith(content, index, format.ItemSeparator))
            {
                builder.Add(content[itemStart..index]);
                index += format.ItemSeparator.Length;
                itemStart = index;
                continue;
            }

            index++;
        }

        if (activeFormat is not null)
        {
            return false;
        }

        builder.Add(content[itemStart..]);
        items = builder.ToImmutable();
        return true;
    }

    private static bool TryParseItem(
        string source,
        ImmutableArray<SemanticCollectionInputItemFormat> formats,
        out SemanticCollectionItem? item)
    {
        item = null;
        var value = source.Trim();
        if (value.Length == 0)
        {
            return false;
        }

        foreach (var format in formats.Where(candidate => candidate.Kind == SemanticCollectionItemKind.Text))
        {
            if (TryDecodeText(value, format, out var text))
            {
                item = new SemanticCollectionItem(SemanticCollectionItemKind.Text, text);
                return true;
            }
        }

        if (formats.Any(candidate =>
                candidate.Kind == SemanticCollectionItemKind.Number &&
                candidate.OpeningDelimiter.Length == 0 &&
                candidate.ClosingDelimiter.Length == 0) &&
            SemanticCollectionValidator.TryNormalizeNumber(value, out var number))
        {
            item = new SemanticCollectionItem(SemanticCollectionItemKind.Number, number);
            return true;
        }

        return false;
    }

    private static bool TryDecodeText(
        string source,
        SemanticCollectionInputItemFormat format,
        out string value)
    {
        value = string.Empty;
        if (format.OpeningDelimiter.Length == 0 ||
            format.ClosingDelimiter.Length == 0 ||
            !source.StartsWith(format.OpeningDelimiter, StringComparison.Ordinal))
        {
            return false;
        }

        var builder = new System.Text.StringBuilder();
        var index = format.OpeningDelimiter.Length;
        while (index < source.Length)
        {
            if (TryMatchEscape(source, index, format, out var escapeLength, out var escapeValue))
            {
                builder.Append(escapeValue);
                index += escapeLength;
                continue;
            }

            if (StartsWith(source, index, format.ClosingDelimiter))
            {
                if (index + format.ClosingDelimiter.Length != source.Length)
                {
                    return false;
                }

                value = builder.ToString();
                return true;
            }

            builder.Append(source[index]);
            index++;
        }

        return false;
    }

    private static bool TryMatchEscape(
        string source,
        int index,
        SemanticCollectionInputItemFormat format,
        out int escapeLength) =>
        TryMatchEscape(source, index, format, out escapeLength, out _);

    private static bool TryMatchEscape(
        string source,
        int index,
        SemanticCollectionInputItemFormat format,
        out int escapeLength,
        out string escapeValue)
    {
        escapeLength = 0;
        escapeValue = string.Empty;
        if (format.Escapes.IsDefaultOrEmpty)
        {
            return false;
        }

        foreach (var escape in format.Escapes.OrderByDescending(item => item.Sequence.Length))
        {
            if (!StartsWith(source, index, escape.Sequence))
            {
                continue;
            }

            escapeLength = escape.Sequence.Length;
            escapeValue = escape.Value;
            return true;
        }

        return false;
    }

    private static bool StartsWith(string value, int index, string prefix) =>
        prefix.Length > 0 &&
        index + prefix.Length <= value.Length &&
        value.AsSpan(index, prefix.Length).SequenceEqual(prefix.AsSpan());
}
