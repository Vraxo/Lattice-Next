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

    public GenerationBooleanLiteralFormat? BooleanLiteralFormat { get; init; }

    /// <summary>
    /// How one member of one instance is written. A pack without this format cannot render a member
    /// access, so member requirements stay unrealizable rather than being emitted with invented syntax.
    /// </summary>
    public GenerationMemberAccessFormat? MemberAccessFormat { get; init; }

    /// <summary>
    /// How a program reads one argument it was started with. A pack without this format cannot render a
    /// program argument, so such requirements stay unrealizable rather than being emitted with invented syntax.
    /// </summary>
    public GenerationProgramArgumentFormat? ProgramArgumentFormat { get; init; }

    /// <summary>
    /// How a literal list of values is written. A pack without this format cannot render a list literal,
    /// so request data stays unrealizable rather than being emitted with invented syntax.
    /// </summary>
    public GenerationCollectionLiteralFormat? CollectionLiteralFormat { get; init; }

    /// <summary>
    /// How a literal record of named fields is written. A pack without this format cannot render a record
    /// literal, so request data stays unrealizable rather than being emitted with invented syntax.
    /// </summary>
    public GenerationRecordLiteralFormat? RecordLiteralFormat { get; init; }
}

/// <summary>The brackets and separator a language writes around the values of a literal list.</summary>
public sealed record GenerationCollectionLiteralFormat(
    string OpeningDelimiter,
    string ClosingDelimiter,
    string Separator);

/// <summary>
/// The brackets, separator, and field spelling a language writes for a literal record, using {0} for the
/// field name and {1} for the field value.
/// </summary>
public sealed record GenerationRecordLiteralFormat(
    string OpeningDelimiter,
    string ClosingDelimiter,
    string Separator,
    string EntryFormat);

/// <summary>The place a language writes a program argument position into, using a {0} placeholder.</summary>
public sealed record GenerationProgramArgumentFormat(
    string Format);

/// <summary>The separator a language writes between an instance and the member it exposes.</summary>
public sealed record GenerationMemberAccessFormat(
    string Separator);

public sealed record GenerationBooleanLiteralFormat(
    string TrueLiteral,
    string FalseLiteral);

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
