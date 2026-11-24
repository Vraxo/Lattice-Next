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
