using System.Text.RegularExpressions;

namespace Lattice.Core.Generation;

/// <summary>Maps comparison phrases in a request to language-neutral operator kinds.</summary>
internal static class ComparisonOperatorNormalizer
{
    internal static bool TryParse(string value, out SemanticExpressionOperator expressionOperator)
    {
        expressionOperator = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var phrase = Regex.Replace(value.Trim(), @"\s+", " ").ToLowerInvariant();
        expressionOperator = phrase switch
        {
            "greater than" or "is greater than" or "more than" => SemanticExpressionOperator.GreaterThan,
            "greater than or equal to" or "is greater than or equal to" or "at least" or "is at least" =>
                SemanticExpressionOperator.GreaterThanOrEqual,
            "less than" or "is less than" or "below" => SemanticExpressionOperator.LessThan,
            "less than or equal to" or "is less than or equal to" or "at most" or "is at most" =>
                SemanticExpressionOperator.LessThanOrEqual,
            "equal" or "equal to" or "equals" or "is equal to" => SemanticExpressionOperator.Equal,
            "not equal to" or "is not equal to" => SemanticExpressionOperator.NotEqual,
            _ => (SemanticExpressionOperator)(-1)
        };

        return expressionOperator is
            SemanticExpressionOperator.Equal or
            SemanticExpressionOperator.NotEqual or
            SemanticExpressionOperator.LessThan or
            SemanticExpressionOperator.LessThanOrEqual or
            SemanticExpressionOperator.GreaterThan or
            SemanticExpressionOperator.GreaterThanOrEqual;
    }
}
