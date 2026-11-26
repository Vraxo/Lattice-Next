using System.Collections.Immutable;

namespace Lattice.Core.Generation;

public sealed record GenerationLanguagePack(
    int FormatVersion,
    string Language,
    string StartSymbol,
    ImmutableArray<GenerationProduction> Productions,
    ImmutableArray<GenerationBinding> Bindings,
    ImmutableArray<GenerationOperation> Operations,
    GenerationStringLiteralFormat StringLiteralFormat,
    GenerationCollectionLiteralFormat? CollectionLiteralFormat = null,
    GenerationInputSyntax? InputSyntax = null,
    GenerationInterpolationFormat? InterpolationFormat = null,
    ImmutableArray<GenerationConversionFormat> ConversionFormats = default)
{
    /// <summary>How a pack spells a literal record of named fields, when it can spell one at all.</summary>
    public GenerationRecordLiteralFormat? RecordLiteralFormat { get; init; }

    /// <summary>
    /// The example values a pack supplies for a request that asks for data without naming it — "a list of
    /// numbers" with no contents, or a total with no starting value. These are the only invented values in
    /// the pipeline: they are declared once here, they are keyed by value kind and never by a task, and a
    /// request that does supply data always wins.
    /// </summary>
    public GenerationExampleData? ExampleData { get; init; }
}

/// <summary>
/// Declared example data for under-specified requests: the sample numbers, the name one number item takes,
/// and the value a number accumulator starts from.
/// </summary>
public sealed record GenerationExampleData(
    ImmutableArray<string> Numbers,
    string NumberItemName,
    string AccumulatorInitialValue)
{
    /// <summary>
    /// The values a request receives when it asks for numbered variables without naming any ("stores two
    /// numbers in variables"). The Nth variable takes the Nth value.
    /// </summary>
    public ImmutableArray<string> NumberVariableValues { get; init; } = ImmutableArray<string>.Empty;

    /// <summary>
    /// The ordinal words that name those variables, so the Nth number variable is `first_number`,
    /// `second_number`, and so on, built from the pack's own item name.
    /// </summary>
    public ImmutableArray<string> Ordinals { get; init; } = ImmutableArray<string>.Empty;

    /// <summary>
    /// The value a request receives when it classifies "an integer" without naming one, as the sign
    /// classification request does.
    /// </summary>
    public string IntegerValue { get; init; } = string.Empty;
}

/// <summary>How a pack spells one text value built from literal fragments and embedded values.</summary>
public sealed record GenerationInterpolationFormat(
    string Opening,
    string PlaceholderOpening,
    string PlaceholderClosing,
    string Closing);

/// <summary>How a pack spells the conversion of a value to one target value kind.</summary>
public sealed record GenerationConversionFormat(
    string Target,
    string Format);
