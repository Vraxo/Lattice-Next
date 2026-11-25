using System.Collections.Immutable;

namespace Lattice.Core.Generation;

/// <summary>
/// Infers the value kind of a semantic expression so declarations, returns, and calls can be checked
/// against each other. The inference is deliberately conservative: anything it cannot prove — a bare
/// name that is not a known parameter, a call to a function the program does not declare, a member
/// access — yields no kind, and no kind is compatible with every declaration.
/// </summary>
internal static class SemanticValueKindAnalysis
{
    public static SemanticValueKind? Infer(
        SemanticExpression? expression,
        ImmutableDictionary<string, SemanticValueKind> names,
        ImmutableDictionary<string, SemanticValueKind> functionResults)
    {
        switch (expression)
        {
            case SemanticNumberExpression:
                return SemanticValueKind.Number;
            case SemanticTextExpression:
                return SemanticValueKind.Text;
            case SemanticBooleanExpression:
                return SemanticValueKind.Boolean;
            case SemanticNameExpression name:
                return names.TryGetValue(name.Name, out var nameKind) ? nameKind : null;
            case SemanticFunctionCallExpression call:
                return functionResults.TryGetValue(call.Name, out var resultKind) ? resultKind : null;
            case SemanticConversionExpression conversion:
                return conversion.Target;
            case SemanticTextTemplateExpression:
                return SemanticValueKind.Text;
            case SemanticUnaryExpression unary:
                return unary.Operator == SemanticExpressionOperator.Not
                    ? SemanticValueKind.Boolean
                    : SemanticValueKind.Number;
            case SemanticBinaryExpression binary:
                return IsBooleanOperator(binary.Operator)
                    ? SemanticValueKind.Boolean
                    : SemanticValueKind.Number;
            default:
                return null;
        }
    }

    /// <summary>A declared kind matches an inferred kind when the inferred kind is unknown or equal.</summary>
    public static bool IsCompatible(SemanticValueKind? inferred, SemanticValueKind declared) =>
        inferred is null || inferred == declared;

    private static bool IsBooleanOperator(SemanticExpressionOperator expressionOperator) =>
        expressionOperator is SemanticExpressionOperator.Equal
            or SemanticExpressionOperator.NotEqual
            or SemanticExpressionOperator.LessThan
            or SemanticExpressionOperator.LessThanOrEqual
            or SemanticExpressionOperator.GreaterThan
            or SemanticExpressionOperator.GreaterThanOrEqual
            or SemanticExpressionOperator.And
            or SemanticExpressionOperator.Or;
}
