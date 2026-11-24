using System.Collections.Immutable;

namespace Lattice.Core.Generation;

public sealed record GenerationInputSyntax(
    string IdentifierStartCharacters,
    string IdentifierContinuationCharacters,
    GenerationNumberLiteralFormat NumberLiteralFormat,
    ImmutableArray<GenerationInputStringLiteralFormat> StringLiteralFormats,
    GenerationFunctionCallFormat FunctionCallFormat)
{
    public ImmutableArray<GenerationDelimiterPair> GroupingDelimiters { get; init; } =
        ImmutableArray<GenerationDelimiterPair>.Empty;

    public ImmutableArray<GenerationInputExpressionOperatorFormat> ExpressionOperators { get; init; } =
        ImmutableArray<GenerationInputExpressionOperatorFormat>.Empty;

    public GenerationDelimiterPair? ExpressionGroupingDelimiter { get; init; }
}

public sealed record GenerationNumberLiteralFormat(
    string Digits,
    string DecimalSeparator,
    string PositiveSign,
    string NegativeSign);

public sealed record GenerationInputStringLiteralFormat(
    string OpeningDelimiter,
    string ClosingDelimiter,
    string EscapePrefix,
    ImmutableArray<GenerationInputEscapeSequence> EscapeSequences,
    bool AllowUnknownEscapes = false,
    bool AllowUnescapedLineBreaks = false);

public sealed record GenerationInputEscapeSequence(
    string Sequence,
    string Value);

public sealed record GenerationInputExpressionOperatorFormat(
    string Kind,
    string Token,
    string Fixity,
    int Precedence);

public sealed record GenerationFunctionCallFormat(
    string OpeningDelimiter,
    string ClosingDelimiter,
    string ArgumentSeparator)
{
    public string? RenderingArgumentSeparator { get; init; }
}

public sealed record GenerationDelimiterPair(
    string OpeningDelimiter,
    string ClosingDelimiter);
