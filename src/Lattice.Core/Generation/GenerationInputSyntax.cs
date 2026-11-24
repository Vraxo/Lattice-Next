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
    ImmutableArray<string> EscapeSequences,
    bool AllowUnknownEscapes = false,
    bool AllowUnescapedLineBreaks = false);

public sealed record GenerationFunctionCallFormat(
    string OpeningDelimiter,
    string ClosingDelimiter,
    string ArgumentSeparator);

public sealed record GenerationDelimiterPair(
    string OpeningDelimiter,
    string ClosingDelimiter);
