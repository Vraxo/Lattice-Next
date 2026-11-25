namespace Lattice.Core.Generation;

internal static class SemanticExpressionRenderer
{
    private const int PrimaryPrecedence = 101;

    public static bool TryRender(
        SemanticExpression expression,
        GenerationLanguagePack? languagePack,
        out string rendered)
    {
        rendered = string.Empty;
        if (languagePack?.InputSyntax is not { } syntax ||
            !SemanticExpressionAnalysis.IsValid(expression) ||
            !TryRender(expression, languagePack, syntax, parentPrecedence: 0, isRightOperand: false, out rendered))
        {
            rendered = string.Empty;
            return false;
        }

        return rendered.Length > 0 && rendered.Length <= 16_384;
    }

    public static bool TryRenderOperator(
        SemanticExpressionOperator expressionOperator,
        GenerationLanguagePack? languagePack,
        out string token)
    {
        token = string.Empty;
        if (languagePack?.InputSyntax is not { } syntax ||
            !TryFindOperator(syntax, expressionOperator, out var format))
        {
            return false;
        }

        token = format.Token;
        return true;
    }

    private static bool TryRender(
        SemanticExpression expression,
        GenerationLanguagePack languagePack,
        GenerationInputSyntax syntax,
        int parentPrecedence,
        bool isRightOperand,
        out string rendered)
    {
        rendered = string.Empty;
        int precedence;
        switch (expression)
        {
            case SemanticNameExpression name:
                if (!GenerationInputSyntaxParser.IsIdentifier(name.Name, syntax))
                {
                    return false;
                }

                rendered = name.Name;
                precedence = PrimaryPrecedence;
                break;
            case SemanticNumberExpression number:
                if (!GenerationInputSyntaxParser.TryFormatNumberLiteral(
                        number.Value, syntax.NumberLiteralFormat, out rendered))
                {
                    return false;
                }

                precedence = PrimaryPrecedence;
                break;
            case SemanticTextExpression text:
                rendered = SequenceGenerationPipeline.EncodeStringLiteral(
                    text.Value, languagePack.StringLiteralFormat);
                precedence = PrimaryPrecedence;
                break;
            case SemanticBooleanExpression boolean:
                if (syntax.BooleanLiteralFormat is not { } booleanFormat)
                {
                    return false;
                }

                rendered = boolean.Value ? booleanFormat.TrueLiteral : booleanFormat.FalseLiteral;
                precedence = PrimaryPrecedence;
                break;
            case SemanticFunctionCallExpression call:
                if (!TryRenderCall(call, languagePack, syntax, out rendered))
                {
                    return false;
                }

                precedence = PrimaryPrecedence;
                break;
            case SemanticMemberAccessExpression member:
                if (!GenerationInputSyntaxParser.IsIdentifier(member.Target, syntax) ||
                    !GenerationInputSyntaxParser.IsIdentifier(member.Member, syntax) ||
                    syntax.MemberAccessFormat is not { } memberAccessFormat)
                {
                    return false;
                }

                rendered = member.Target + memberAccessFormat.Separator + member.Member;
                precedence = PrimaryPrecedence;
                break;
            case SemanticUnaryExpression unary:
                if (!TryFindOperator(syntax, unary.Operator, out var prefixFormat) ||
                    prefixFormat.Fixity != "prefix" ||
                    !TryRender(
                        unary.Operand,
                        languagePack,
                        syntax,
                        prefixFormat.Precedence,
                        isRightOperand: true,
                        out var operand))
                {
                    return false;
                }

                var separator = prefixFormat.Token.Any(char.IsLetter) ? " " : string.Empty;
                rendered = prefixFormat.Token + separator + operand;
                precedence = prefixFormat.Precedence;
                break;
            case SemanticBinaryExpression binary:
                if (!TryFindOperator(syntax, binary.Operator, out var infixFormat) ||
                    infixFormat.Fixity != "infix" ||
                    !TryRender(
                        binary.Left,
                        languagePack,
                        syntax,
                        infixFormat.Precedence,
                        isRightOperand: false,
                        out var left) ||
                    !TryRender(
                        binary.Right,
                        languagePack,
                        syntax,
                        infixFormat.Precedence,
                        isRightOperand: true,
                        out var right))
                {
                    return false;
                }

                rendered = $"{left} {infixFormat.Token} {right}";
                precedence = infixFormat.Precedence;
                break;
            default:
                return false;
        }

        if (precedence < parentPrecedence ||
            (precedence == parentPrecedence && isRightOperand && expression is SemanticBinaryExpression))
        {
            if (!TryGroup(rendered, syntax, out var grouped))
            {
                return false;
            }

            rendered = grouped;
        }

        return rendered.Length <= 16_384;
    }

    private static bool TryRenderCall(
        SemanticFunctionCallExpression call,
        GenerationLanguagePack languagePack,
        GenerationInputSyntax syntax,
        out string rendered)
    {
        rendered = string.Empty;
        if (!GenerationInputSyntaxParser.IsIdentifier(call.Name, syntax))
        {
            return false;
        }

        var format = syntax.FunctionCallFormat;
        var arguments = new List<string>(call.Arguments.Length);
        foreach (var argument in call.Arguments)
        {
            if (!TryRender(argument, languagePack, syntax, 0, isRightOperand: false, out var renderedArgument))
            {
                return false;
            }

            arguments.Add(renderedArgument);
        }

        var separator = format.RenderingArgumentSeparator ?? format.ArgumentSeparator;
        rendered = call.Name + format.OpeningDelimiter +
            string.Join(separator, arguments) + format.ClosingDelimiter;
        return rendered.Length <= 16_384;
    }

    private static bool TryGroup(
        string expression,
        GenerationInputSyntax syntax,
        out string grouped)
    {
        grouped = string.Empty;
        var delimiter = syntax.ExpressionGroupingDelimiter;
        if (delimiter is null ||
            string.IsNullOrEmpty(delimiter.OpeningDelimiter) ||
            string.IsNullOrEmpty(delimiter.ClosingDelimiter))
        {
            return false;
        }

        grouped = delimiter.OpeningDelimiter + expression + delimiter.ClosingDelimiter;
        return grouped.Length <= 16_384;
    }

    private static bool TryFindOperator(
        GenerationInputSyntax syntax,
        SemanticExpressionOperator expressionOperator,
        out GenerationInputExpressionOperatorFormat format)
    {
        var kind = SemanticExpressionParser.GetOperatorKind(expressionOperator);
        format = syntax.ExpressionOperators.FirstOrDefault(candidate => candidate.Kind == kind)!;
        return format is not null;
    }
}
