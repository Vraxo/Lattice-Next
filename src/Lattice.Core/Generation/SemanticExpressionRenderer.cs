using System.Globalization;
using System.Collections.Immutable;

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

    /// <summary>
    /// Renders one entry value of a record or map literal. A literal style may spell plain text values its own
    /// way, such as Python's dict style quoting every string with single quotes; anything the style cannot spell
    /// safely keeps the language's canonical string literal.
    /// </summary>
    private static bool TryRenderLiteralValue(
        SemanticExpression value,
        GenerationRecordLiteralFormat literal,
        GenerationLanguagePack languagePack,
        GenerationInputSyntax syntax,
        out string rendered)
    {
        rendered = string.Empty;
        if (literal.TextFormat is { } textFormat &&
            value is SemanticTextExpression text &&
            text.Value.All(character => character is not ('\'' or '"' or '\\' or '\n' or '\r')))
        {
            rendered = textFormat.Replace("{0}", text.Value, StringComparison.Ordinal);
            return true;
        }

        return TryRender(value, languagePack, syntax, parentPrecedence: 0, isRightOperand: false, out rendered);
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
                if (!GenerationInputSyntaxParser.IsIdentifier(member.Member, syntax) ||
                    syntax.MemberAccessFormat is not { } memberAccessFormat ||
                    !TryRender(
                        member.Target,
                        languagePack,
                        syntax,
                        PrimaryPrecedence,
                        isRightOperand: false,
                        out var memberTarget))
                {
                    return false;
                }

                rendered = memberTarget + memberAccessFormat.Separator + member.Member;
                precedence = PrimaryPrecedence;
                break;
            case SemanticProgramArgumentExpression programArgument:
                if (syntax.ProgramArgumentFormat is not { } programArgumentFormat ||
                    !programArgumentFormat.Format.Contains("{0}", StringComparison.Ordinal))
                {
                    return false;
                }

                rendered = programArgumentFormat.Format.Replace(
                    "{0}",
                    programArgument.Position.ToString(CultureInfo.InvariantCulture),
                    StringComparison.Ordinal);
                precedence = PrimaryPrecedence;
                break;
            case SemanticListLiteralExpression list:
                if (languagePack.CollectionLiteralFormat is not { } collectionLiteral ||
                    !TryRenderLiteralItems(
                        list.Items,
                        languagePack,
                        syntax,
                        collectionLiteral.ItemSeparator,
                        out var renderedItems))
                {
                    return false;
                }

                rendered = collectionLiteral.OpeningDelimiter +
                    renderedItems +
                    collectionLiteral.ClosingDelimiter;
                precedence = PrimaryPrecedence;
                break;
            case SemanticRecordLiteralExpression record:
                if (languagePack.RecordLiteralFormat is not { } recordLiteral ||
                    !recordLiteral.EntryFormat.Contains("{0}", StringComparison.Ordinal) ||
                    !recordLiteral.EntryFormat.Contains("{1}", StringComparison.Ordinal))
                {
                    return false;
                }

                var entries = ImmutableArray.CreateBuilder<string>();
                foreach (var entry in record.Entries)
                {
                    if (!TryRenderLiteralValue(
                            entry.Value,
                            recordLiteral,
                            languagePack,
                            syntax,
                            out var renderedValue))
                    {
                        return false;
                    }

                    entries.Add(recordLiteral.EntryFormat
                        .Replace("{0}", entry.Name, StringComparison.Ordinal)
                        .Replace("{1}", renderedValue, StringComparison.Ordinal));
                }

                rendered = recordLiteral.OpeningDelimiter +
                    string.Join(recordLiteral.EntrySeparator, entries) +
                    recordLiteral.ClosingDelimiter;
                precedence = PrimaryPrecedence;
                break;
            case SemanticMapLiteralExpression map:
                if (languagePack.MapLiteralFormat is not { } mapLiteral ||
                    !mapLiteral.EntryFormat.Contains("{0}", StringComparison.Ordinal) ||
                    !mapLiteral.EntryFormat.Contains("{1}", StringComparison.Ordinal))
                {
                    return false;
                }

                var mapEntries = ImmutableArray.CreateBuilder<string>();
                foreach (var entry in map.Entries)
                {
                    if (!TryRenderLiteralValue(
                            entry.Value,
                            mapLiteral,
                            languagePack,
                            syntax,
                            out var renderedMapValue))
                    {
                        return false;
                    }

                    mapEntries.Add(mapLiteral.EntryFormat
                        .Replace("{0}", entry.Name, StringComparison.Ordinal)
                        .Replace("{1}", renderedMapValue, StringComparison.Ordinal));
                }

                rendered = mapLiteral.OpeningDelimiter +
                    string.Join(mapLiteral.EntrySeparator, mapEntries) +
                    mapLiteral.ClosingDelimiter;
                precedence = PrimaryPrecedence;
                break;
            case SemanticLengthExpression length:
                if (languagePack.LengthFormat is not { } lengthFormat ||
                    !lengthFormat.Contains("{0}", StringComparison.Ordinal) ||
                    !TryRender(
                        length.Value,
                        languagePack,
                        syntax,
                        parentPrecedence: 0,
                        isRightOperand: false,
                        out var renderedLengthOperand))
                {
                    return false;
                }

                rendered = lengthFormat.Replace("{0}", renderedLengthOperand, StringComparison.Ordinal);
                precedence = PrimaryPrecedence;
                break;
            case SemanticPlaceholderMappingExpression placeholder:
                if (languagePack.PlaceholderMappingFormat is not { } placeholderFormat ||
                    !placeholderFormat.Contains("{0}", StringComparison.Ordinal))
                {
                    return false;
                }

                rendered = placeholderFormat.Replace("{0}", placeholder.Note, StringComparison.Ordinal);
                precedence = PrimaryPrecedence;
                break;
            case SemanticTotalExpression total:
                if (!TryRenderTotal(total, languagePack, syntax, out rendered))
                {
                    return false;
                }

                precedence = PrimaryPrecedence;
                break;
            case SemanticFilterExpression filter:
                if (!TryRenderFilter(filter, languagePack, syntax, out rendered))
                {
                    return false;
                }

                precedence = PrimaryPrecedence;
                break;
            case SemanticGeneratorExpression generator:
                if (languagePack.GeneratorExpressionFormat is not { } generatorFormat ||
                    !generatorFormat.Contains("{0}", StringComparison.Ordinal) ||
                    !generatorFormat.Contains("{1}", StringComparison.Ordinal) ||
                    !generatorFormat.Contains("{2}", StringComparison.Ordinal) ||
                    !TryRender(generator.Value, languagePack, syntax, parentPrecedence: 0, isRightOperand: false, out var renderedGeneratorValue) ||
                    !TryRender(generator.Source, languagePack, syntax, parentPrecedence: 0, isRightOperand: false, out var renderedGeneratorSource))
                {
                    return false;
                }

                rendered = generatorFormat
                    .Replace("{0}", renderedGeneratorValue, StringComparison.Ordinal)
                    .Replace("{1}", generator.ItemName, StringComparison.Ordinal)
                    .Replace("{2}", renderedGeneratorSource, StringComparison.Ordinal);
                precedence = PrimaryPrecedence;
                break;
            case SemanticTupleExpression tuple:
                if (languagePack.TupleFormat is not { } tupleFormat ||
                    !tupleFormat.Contains("{0}", StringComparison.Ordinal) ||
                    !TryRender(tuple.Value, languagePack, syntax, parentPrecedence: 0, isRightOperand: false, out var renderedTupleValue))
                {
                    return false;
                }

                rendered = tupleFormat.Replace("{0}", renderedTupleValue, StringComparison.Ordinal);
                precedence = PrimaryPrecedence;
                break;
            case SemanticSubscriptExpression subscript:
                if (languagePack.SubscriptFormat is not { } subscriptFormat ||
                    !subscriptFormat.Contains("{0}", StringComparison.Ordinal) ||
                    !subscriptFormat.Contains("{1}", StringComparison.Ordinal) ||
                    !TryRender(subscript.Target, languagePack, syntax, PrimaryPrecedence, false, out var renderedTarget) ||
                    !TryRender(subscript.Key, languagePack, syntax, parentPrecedence: 0, isRightOperand: false, out var renderedKey))
                {
                    return false;
                }

                rendered = subscriptFormat
                    .Replace("{0}", renderedTarget, StringComparison.Ordinal)
                    .Replace("{1}", renderedKey, StringComparison.Ordinal);
                precedence = PrimaryPrecedence;
                break;
            case SemanticCaseFoldExpression caseFold:
                if (languagePack.CaseFoldFormat is not { } caseFoldFormat ||
                    !caseFoldFormat.Contains("{0}", StringComparison.Ordinal) ||
                    !TryRender(caseFold.Value, languagePack, syntax, PrimaryPrecedence, false, out var renderedCaseFold))
                {
                    return false;
                }

                rendered = caseFoldFormat.Replace("{0}", renderedCaseFold, StringComparison.Ordinal);
                precedence = PrimaryPrecedence;
                break;
            case SemanticSortExpression sort:
                var sortFormat = sort.Descending
                    ? languagePack.SortedExpressionReverseFormat
                    : languagePack.SortedExpressionFormat;
                if (sortFormat is not { } orderingFormat ||
                    !orderingFormat.Contains("{0}", StringComparison.Ordinal) ||
                    !orderingFormat.Contains("{1}", StringComparison.Ordinal) ||
                    !orderingFormat.Contains("{2}", StringComparison.Ordinal) ||
                    !TryRender(sort.Source, languagePack, syntax, parentPrecedence: 0, isRightOperand: false, out var renderedSortSource) ||
                    !TryRender(sort.Key, languagePack, syntax, parentPrecedence: 0, isRightOperand: false, out var renderedSortKey))
                {
                    return false;
                }

                rendered = orderingFormat
                    .Replace("{0}", renderedSortSource, StringComparison.Ordinal)
                    .Replace("{1}", sort.ItemName, StringComparison.Ordinal)
                    .Replace("{2}", renderedSortKey, StringComparison.Ordinal);
                precedence = PrimaryPrecedence;
                break;
            case SemanticArgumentsExpression arguments:
                if (!TryRenderLiteralItems(
                        arguments.Values,
                        languagePack,
                        syntax,
                        ", ",
                        out var renderedArguments))
                {
                    return false;
                }

                rendered = renderedArguments;
                precedence = PrimaryPrecedence;
                break;
            case SemanticMethodCallExpression methodCall:
                if (!TryRenderMethodCall(methodCall, languagePack, syntax, out rendered))
                {
                    return false;
                }

                precedence = PrimaryPrecedence;
                break;
            case SemanticConversionExpression conversion:
                if (!TryRender(
                        conversion.Value,
                        languagePack,
                        syntax,
                        parentPrecedence: 0,
                        isRightOperand: false,
                        out var converted) ||
                    !TryFormatConversion(conversion.Target, converted, languagePack, out rendered))
                {
                    return false;
                }

                precedence = PrimaryPrecedence;
                break;
            case SemanticTextTemplateExpression template:
                if (!TryRenderTextTemplate(template, languagePack, syntax, out rendered))
                {
                    return false;
                }

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

    private static bool TryRenderArguments(
        ImmutableArray<SemanticExpression> arguments,
        GenerationLanguagePack languagePack,
        GenerationInputSyntax syntax,
        out string rendered)
    {
        rendered = string.Empty;
        var format = syntax.FunctionCallFormat;
        var parts = new List<string>(arguments.Length);
        foreach (var argument in arguments)
        {
            if (!TryRender(argument, languagePack, syntax, 0, isRightOperand: false, out var renderedArgument))
            {
                return false;
            }

            parts.Add(renderedArgument);
        }

        var separator = format.RenderingArgumentSeparator ?? format.ArgumentSeparator;
        rendered = string.Join(separator, parts);
        return rendered.Length <= 16_384;
    }

    private static bool TryRenderLiteralItems(
        ImmutableArray<SemanticExpression> items,
        GenerationLanguagePack languagePack,
        GenerationInputSyntax syntax,
        string separator,
        out string rendered)
    {
        rendered = string.Empty;
        var values = ImmutableArray.CreateBuilder<string>();
        foreach (var item in items)
        {
            if (!TryRender(
                    item,
                    languagePack,
                    syntax,
                    parentPrecedence: 0,
                    isRightOperand: false,
                    out var renderedItem))
            {
                return false;
            }

            values.Add(renderedItem);
        }

        rendered = string.Join(separator, values);
        return true;
    }

    private static bool TryRenderTotal(
        SemanticTotalExpression total,
        GenerationLanguagePack languagePack,
        GenerationInputSyntax syntax,
        out string rendered)
    {
        rendered = string.Empty;
        if (languagePack.TotalExpressionFormat is not { } totalFormat ||
            !totalFormat.Contains("{0}", StringComparison.Ordinal) ||
            !TryRender(
                total.Source,
                languagePack,
                syntax,
                parentPrecedence: 0,
                isRightOperand: false,
                out var renderedSource))
        {
            return false;
        }

        rendered = totalFormat.Replace("{0}", renderedSource, StringComparison.Ordinal);
        return true;
    }

    private static bool TryRenderFilter(
        SemanticFilterExpression filter,
        GenerationLanguagePack languagePack,
        GenerationInputSyntax syntax,
        out string rendered)
    {
        rendered = string.Empty;
        if (languagePack.FilterExpressionFormat is not { } filterFormat ||
            !filterFormat.Contains("{0}", StringComparison.Ordinal) ||
            !filterFormat.Contains("{1}", StringComparison.Ordinal) ||
            !filterFormat.Contains("{2}", StringComparison.Ordinal) ||
            !filterFormat.Contains("{3}", StringComparison.Ordinal) ||
            !TryRender(
                filter.Source,
                languagePack,
                syntax,
                parentPrecedence: 0,
                isRightOperand: false,
                out var renderedSource) ||
            !TryRender(
                filter.Predicate,
                languagePack,
                syntax,
                parentPrecedence: 0,
                isRightOperand: false,
                out var renderedPredicate))
        {
            return false;
        }

        rendered = filterFormat
            .Replace("{0}", filter.ItemName, StringComparison.Ordinal)
            .Replace("{1}", filter.ItemName, StringComparison.Ordinal)
            .Replace("{2}", renderedSource, StringComparison.Ordinal)
            .Replace("{3}", renderedPredicate, StringComparison.Ordinal);
        return true;
    }

    private static bool TryRenderMethodCall(
        SemanticMethodCallExpression call,
        GenerationLanguagePack languagePack,
        GenerationInputSyntax syntax,
        out string rendered)
    {
        rendered = string.Empty;
        if (!GenerationInputSyntaxParser.IsIdentifier(call.Method, syntax) ||
            syntax.MemberAccessFormat is not { } memberAccessFormat ||
            !TryRender(
                call.Target,
                languagePack,
                syntax,
                PrimaryPrecedence,
                isRightOperand: false,
                out var target) ||
            !TryRenderArguments(call.Arguments, languagePack, syntax, out var arguments))
        {
            return false;
        }

        var format = syntax.FunctionCallFormat;
        rendered = target + memberAccessFormat.Separator + call.Method +
            format.OpeningDelimiter + arguments + format.ClosingDelimiter;
        return true;
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

        if (!TryRenderArguments(call.Arguments, languagePack, syntax, out var arguments))
        {
            return false;
        }

        var format = syntax.FunctionCallFormat;
        rendered = call.Name + format.OpeningDelimiter + arguments + format.ClosingDelimiter;
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

    /// <summary>
    /// Spells the conversion of one rendered value using the pack's format for the target kind, so a
    /// request never names a language construct.
    /// </summary>
    private static bool TryFormatConversion(
        SemanticValueKind target,
        string renderedValue,
        GenerationLanguagePack languagePack,
        out string rendered)
    {
        rendered = string.Empty;
        if (languagePack.ConversionFormats.IsDefaultOrEmpty)
        {
            return false;
        }

        var format = languagePack.ConversionFormats.FirstOrDefault(candidate =>
            string.Equals(candidate.Target, target.ToString(), StringComparison.Ordinal));
        if (format is null)
        {
            return false;
        }

        rendered = format.Format.Replace("{0}", renderedValue, StringComparison.Ordinal);
        return rendered.Length > 0;
    }

    /// <summary>
    /// Spells one text value built from literal fragments and embedded values using the pack's
    /// interpolation format.
    /// </summary>
    private static bool TryRenderTextTemplate(
        SemanticTextTemplateExpression template,
        GenerationLanguagePack languagePack,
        GenerationInputSyntax syntax,
        out string rendered)
    {
        rendered = string.Empty;
        if (languagePack.InterpolationFormat is not { } interpolation ||
            template.Parts.IsDefaultOrEmpty)
        {
            return false;
        }

        var builder = new System.Text.StringBuilder(interpolation.Opening);
        foreach (var part in template.Parts)
        {
            if (part is SemanticTextExpression text)
            {
                var literal = SequenceGenerationPipeline.EncodeStringLiteral(
                    text.Value,
                    languagePack.StringLiteralFormat);
                var opening = languagePack.StringLiteralFormat.OpeningDelimiter;
                var closing = languagePack.StringLiteralFormat.ClosingDelimiter;
                if (literal.Length < opening.Length + closing.Length)
                {
                    return false;
                }

                builder.Append(literal, opening.Length, literal.Length - opening.Length - closing.Length);
                continue;
            }

            if (!TryRender(part, languagePack, syntax, parentPrecedence: 0, isRightOperand: false, out var value))
            {
                return false;
            }

            builder.Append(interpolation.PlaceholderOpening).Append(value).Append(interpolation.PlaceholderClosing);
        }

        builder.Append(interpolation.Closing);
        rendered = builder.ToString();
        return true;
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
