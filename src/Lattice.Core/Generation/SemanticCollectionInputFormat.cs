using System.Collections.Immutable;

namespace Lattice.Core.Generation;

public sealed record SemanticCollectionInputFormat(
    string OpeningDelimiter,
    string ClosingDelimiter,
    string ItemSeparator,
    ImmutableArray<SemanticCollectionInputItemFormat> ItemFormats);

public sealed record SemanticCollectionInputItemFormat(
    SemanticCollectionItemKind Kind,
    string OpeningDelimiter,
    string ClosingDelimiter,
    ImmutableArray<SemanticCollectionInputEscape> Escapes);

public sealed record SemanticCollectionInputEscape(
    string Sequence,
    string Value);
