using System.Text.RegularExpressions;

namespace Lattice.Core.Generation;

internal static class ComparisonOperatorNormalizer
{
    internal static bool TryNormalize(string value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var phrase = Regex.Replace(value.Trim(), @"\s+", " ").ToLowerInvariant();
        normalized = phrase switch
        {
            ">" or "greater than" or "more than" => ">",
            ">=" or "greater than or equal to" or "at least" => ">=",
            "<" or "less than" or "below" => "<",
            "<=" or "less than or equal to" or "at most" => "<=",
            "==" or "equal to" or "equals" or "is equal to" => "==",
            "!=" or "not equal to" or "is not equal to" => "!=",
            _ => string.Empty
        };

        return normalized.Length > 0;
    }
}
