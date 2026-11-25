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
