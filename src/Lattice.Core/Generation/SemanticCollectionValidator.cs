using System.Globalization;

namespace Lattice.Core.Generation;

internal static class SemanticCollectionValidator
{
    public const int MaximumItems = 128;
    public const int MaximumItemValueLength = 16_384;

    public static bool IsValid(SemanticCollection? collection)
    {
        if (collection is null ||
            collection.Items.IsDefault ||
            collection.Items.Length > MaximumItems)
        {
            return false;
        }

        foreach (var item in collection.Items)
        {
            if (item is null ||
                item.Value is null ||
                item.Value.Length > MaximumItemValueLength)
            {
                return false;
            }

            if (item.Kind == SemanticCollectionItemKind.Number)
            {
                if (!TryNormalizeNumber(item.Value, out _))
                {
                    return false;
                }
            }
            else if (item.Kind != SemanticCollectionItemKind.Text)
            {
                return false;
            }
        }

        return true;
    }

    public static bool TryNormalizeNumber(string value, out string normalized)
    {
        normalized = string.Empty;
        const NumberStyles styles = NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint;
        if (string.IsNullOrWhiteSpace(value) ||
            !decimal.TryParse(value, styles, CultureInfo.InvariantCulture, out var number))
        {
            return false;
        }

        normalized = number.ToString(CultureInfo.InvariantCulture);
        return true;
    }
}
