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
    /// How this pack writes a mapping literal, such as Python's `{'key': value}`. The entry format carries
    /// `{0}` for the request's key text and `{1}` for the rendered value.
    /// </summary>
    public GenerationRecordLiteralFormat? MapLiteralFormat { get; init; }

    /// <summary>
    /// How this pack spells the values a knowledge-declared API contract names, one entry per contract. A
    /// contract the pack does not declare cannot be written in this language, so the requirement stays
    /// unrealizable instead of being emitted with an invented spelling.
    /// </summary>
    public ImmutableArray<GenerationApiExpressionFormat> ApiExpressionFormats { get; init; } =
        ImmutableArray<GenerationApiExpressionFormat>.Empty;

    /// <summary>How this pack spells the length of a text value; the format carries `{0}` for the operand.</summary>
    public string? LengthFormat { get; init; }

    /// <summary>How this pack reads one value from another by key; the format carries `{0}` value and `{1}` key.</summary>
    public string? SubscriptFormat { get; init; }

    /// <summary>How this pack spells a text value's case-insensitive form; `{0}` is the operand.</summary>
    public string? CaseFoldFormat { get; init; }

    /// <summary>How this pack orders a collection by one key expression; `{0}` items, `{1}` item name, `{2}` key.</summary>
    public string? SortedExpressionFormat { get; init; }

    /// <summary>The descending form of <see cref="SortedExpressionFormat"/>.</summary>
    public string? SortedExpressionReverseFormat { get; init; }

    /// <summary>How this pack spells one value per item; `{0}` value, `{1}` item name, `{2}` source.</summary>
    /// <summary>
    /// How this language writes a mapping a program has yet to fill in, with `{0}` the note its reader sees.
    /// </summary>
    public string? PlaceholderMappingFormat { get; init; }

    /// <summary>The total of the values a collection holds, spelled by this language.</summary>
    public string? TotalExpressionFormat { get; init; }

    /// <summary>
    /// How this language counts from one number up to another; the format carries `{0}` for the start and
    /// `{1}` for the exclusive end.
    /// </summary>
    public string? RangeExpressionFormat { get; init; }

    /// <summary>
    /// How this language writes a collection of the items that satisfy a predicate, with `{0}` the kept value,
    /// `{1}` the item, `{2}` the source, and `{3}` the predicate.
    /// </summary>
    public string? FilterExpressionFormat { get; init; }

    public string? GeneratorExpressionFormat { get; init; }

    /// <summary>How this pack groups several values into one ordered value; `{0}` is the operand.</summary>
    public string? TupleFormat { get; init; }


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

    /// <summary>
    /// The note a plan attaches to a mapping it leaves for the reader to fill in, when the request states no data.
    /// </summary>
    public string? EmptyMappingNote { get; init; }

    /// <summary>The values a plan reports when a request states none; part of the same single policy.</summary>
    public GenerationSampleCallPolicy? SampleCall { get; init; }
}

/// <summary>
/// The values a planned program reports when the request states none: the name its function takes and the
/// arguments it demonstrates. Keyed by value kind only, never by a request, so one policy serves every family
/// that ends in a demonstration call.
/// </summary>
public sealed record GenerationSampleCallPolicy(
    string FunctionName,
    string ResultName,
    ImmutableArray<string> Numbers,
    ImmutableArray<string> Texts)
{
    /// <summary>The record-shaped demonstration a sorting plan prints; absent when a language has no sorts.</summary>
    public GenerationSampleSortPolicy? Sort { get; init; }

    /// <summary>
    /// The records a grouped-total plan adds up when the request states no data: the names it defaults to and
    /// the group labels and amounts of its sample records.
    /// </summary>
    public GenerationSampleGroupedAggregation? GroupedAggregation { get; init; }
}

/// <summary>
/// The demonstration a grouped-total plan prints: the names it defaults to and, per sample record, the group
/// label and the amount that record contributes. Both lists have one entry per record, in record order.
/// </summary>
public sealed record GenerationSampleGroupedAggregation(
    string RecordsName,
    string TotalsName,
    string KeyName,
    string TotalName,
    ImmutableArray<string> Groups,
    ImmutableArray<string> Measures);

/// <summary>
/// The records a sorting plan demonstrates when the request supplies no data: one record shape, and one
/// sample per sensitivity. The shape is the ordered field list, so a sample record is a list of values rather
/// than a mapping, and the field a sample sorts by is one of those names.
/// </summary>
public sealed record GenerationSampleSortPolicy(
    string FunctionName,
    string RecordsName,
    string FieldName,
    string ItemName,
    ImmutableArray<GenerationSampleField> Fields,
    ImmutableArray<GenerationRecordSortSample> Samples)
{
    /// <summary>The several-key demonstration, when a language can order by more than one field.</summary>
    public GenerationSampleMultiFieldSort? MultiField { get; init; }
}

/// <summary>
/// The demonstration a several-key sort prints: the names it defaults to, the generator's item name, the record
/// shape, the keys it orders by, and the records themselves in field order.
/// </summary>
public sealed record GenerationSampleMultiFieldSort(
    string FunctionName,
    string RecordsName,
    string KeysName,
    string ItemName,
    string KeyItemName,
    ImmutableArray<GenerationSampleField> Fields,
    ImmutableArray<string> SortKeys,
    ImmutableArray<ImmutableArray<string>> Records,
    string RecordsLiteral);

/// <summary>One field of the sample record shape and whether it holds text or a number.</summary>
public sealed record GenerationSampleField(string Name, bool IsText);

/// <summary>
/// One demonstrated sort: the field it orders by, whether it orders case-insensitively, which declared literal
/// format spells its records, and the records themselves in field order.
/// </summary>
public sealed record GenerationRecordSortSample(
    string Field,
    bool CaseInsensitive,
    string RecordsLiteral,
    ImmutableArray<ImmutableArray<string>> Records);

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

/// <summary>One knowledge-declared API contract and the spelling this language gives it.</summary>
public sealed record GenerationApiExpressionFormat(
    string Contract,
    string Format)
{
    /// <summary>Exact arity; each argument has an indexed placeholder in the declared format.</summary>
    public int ArgumentCount { get; init; }
}
