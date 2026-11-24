using System.Text;

namespace Lattice.Core.Generation;

internal static class SemanticCollectionLiteralRenderer
{
    private const int MaximumRenderedLength = 16_384;

    public static bool TryRender(
        SemanticCollection collection,
        GenerationLanguagePack pack,
        out string rendered)
    {
        rendered = string.Empty;
        if (!SemanticCollectionValidator.IsValid(collection) ||
            pack.CollectionLiteralFormat is not { } format ||
            format.ItemFormats.IsDefaultOrEmpty)
        {
            return false;
        }

        var itemFormats = new Dictionary<string, GenerationCollectionItemFormat>(StringComparer.Ordinal);
        foreach (var itemFormat in format.ItemFormats)
        {
            if (itemFormat is null ||
                itemFormat.Kind is not ("number" or "text") ||
                !itemFormats.TryAdd(itemFormat.Kind, itemFormat))
            {
                return false;
            }
        }

        var renderedItems = new List<string>(collection.Items.Length);
        foreach (var item in collection.Items)
        {
            string renderedItem;
            switch (item.Kind)
            {
                case SemanticCollectionItemKind.Number:
                    if (!itemFormats.TryGetValue("number", out var numberFormat) ||
                        numberFormat.Encoding != "none" ||
                        !SemanticCollectionValidator.TryNormalizeNumber(item.Value, out renderedItem))
                    {
                        return false;
                    }

                    break;
                case SemanticCollectionItemKind.Text:
                    if (!itemFormats.TryGetValue("text", out var textFormat) ||
                        textFormat.Encoding != "string-literal")
                    {
                        return false;
                    }

                    renderedItem = SequenceGenerationPipeline.EncodeStringLiteral(
                        item.Value,
                        pack.StringLiteralFormat);
                    break;
                default:
                    return false;
            }

            renderedItems.Add(renderedItem);
        }

        var renderedLength = format.OpeningDelimiter.Length + format.ClosingDelimiter.Length +
            renderedItems.Sum(item => item.Length) +
            Math.Max(0, renderedItems.Count - 1) * format.ItemSeparator.Length;
        if (renderedLength > MaximumRenderedLength)
        {
            return false;
        }

        var builder = new StringBuilder(renderedLength);
        builder.Append(format.OpeningDelimiter);
        builder.Append(string.Join(format.ItemSeparator, renderedItems));
        builder.Append(format.ClosingDelimiter);
        rendered = builder.ToString();
        return true;
    }
}
