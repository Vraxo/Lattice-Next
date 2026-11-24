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
    GenerationCollectionLiteralFormat? CollectionLiteralFormat = null);
