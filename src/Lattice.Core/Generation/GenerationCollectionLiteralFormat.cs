using System.Collections.Immutable;

namespace Lattice.Core.Generation;

public sealed record GenerationCollectionLiteralFormat(
    string OpeningDelimiter,
    string ClosingDelimiter,
    string ItemSeparator,
    ImmutableArray<GenerationCollectionItemFormat> ItemFormats);

public sealed record GenerationCollectionItemFormat(
    string Kind,
    string Encoding);

/// <summary>
/// The brackets, separator, and field spelling a language writes for a literal record, using {0} for the
/// field name and {1} for the field value. A pack without this format cannot render a record literal, so
/// request data stays unrealizable rather than being emitted with invented syntax.
/// </summary>
public sealed record GenerationRecordLiteralFormat(
    string OpeningDelimiter,
    string ClosingDelimiter,
    string EntrySeparator,
    string EntryFormat)
{
    /// <summary>
    /// How a text value inside this literal is spelled, when the literal's own style quotes strings differently
    /// from the language's canonical string literal. `{0}` carries the text.
    /// </summary>
    public string? TextFormat { get; init; }
}
