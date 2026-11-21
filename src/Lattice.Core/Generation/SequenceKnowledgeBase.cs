using System.Collections.Immutable;

namespace Lattice.Core.Generation;

public sealed record SequenceSemanticFact(
    string Id,
    ImmutableDictionary<string, string> Values)
{
    public ImmutableDictionary<string, ImmutableArray<string>> Collections { get; init; } =
        ImmutableDictionary<string, ImmutableArray<string>>.Empty.WithComparers(StringComparer.Ordinal);
}

public sealed record SequenceTokenBinding(
    string FactId,
    string ValueId,
    string? Encoding = null,
    bool UseCollection = false)
{
    public string? CollectionSeparator { get; init; }

    public string Prefix { get; init; } = string.Empty;

    public string Suffix { get; init; } = string.Empty;

    public string? SecondaryValueId { get; init; }

    public string? SecondaryCollectionSeparator { get; init; }

    public string? ValueTemplate { get; init; }

    public int MinimumCollectionCursor { get; init; }
}

public sealed record SequenceTokenEscape(
    string Character,
    string Replacement);

public sealed record SequenceTokenEncoding(
    string Id,
    string OpeningDelimiter,
    string ClosingDelimiter,
    ImmutableArray<SequenceTokenEscape> Escapes);

public sealed record SequenceTokenRule(
    string Id,
    ImmutableArray<string> ContextSuffix,
    ImmutableArray<string> RequiredFacts,
    ImmutableArray<string> RequiresCompletedFacts,
    string? NextToken,
    ImmutableArray<string> SatisfiesFacts,
    double Weight = 0)
{
    public SequenceTokenBinding? TokenBinding { get; init; }

    public ImmutableArray<string> AdvanceCollections { get; init; } = ImmutableArray<string>.Empty;

    public ImmutableArray<string> ForbiddenFacts { get; init; } = ImmutableArray<string>.Empty;
}

public sealed record SequenceInputFactCapture(
    string FactId,
    string ValueId,
    ImmutableArray<string> StartAfterTokens,
    ImmutableArray<string> StopBeforeTokens,
    string? LiteralValue = null)
{
    public string? CollectionDelimiter { get; init; }

    public ImmutableArray<string> CollectionDelimiters { get; init; } = ImmutableArray<string>.Empty;

    public ImmutableArray<ImmutableArray<string>> StartAfterSequences { get; init; } =
        ImmutableArray<ImmutableArray<string>>.Empty;

    public ImmutableArray<ImmutableArray<string>> StopBeforeSequences { get; init; } =
        ImmutableArray<ImmutableArray<string>>.Empty;

    public string? CollectionFieldDelimiter { get; init; }

    public int? CollectionFieldIndex { get; init; }

    public string? CollectionTemplate { get; init; }

    public ImmutableArray<string> TrimPrefixTokens { get; init; } = ImmutableArray<string>.Empty;

    public string TrimSuffixCharacters { get; init; } = string.Empty;

    public bool PreserveRawValue { get; init; }
}

public sealed record SequenceInputFactValidation(
    string Id,
    string Kind,
    string FactId,
    string ValueId,
    string? RelatedFactId = null,
    string? RelatedValueId = null,
    string? AdditionalFactId = null,
    string? AdditionalValueId = null)
{
    public string? AppliesWhenFactId { get; init; }
}

public sealed record SequenceInputFactRule(
    string Id,
    ImmutableArray<string> MatchTokens,
    string FactId,
    string? ValueId = null,
    string? LiteralValue = null,
    ImmutableArray<SequenceInputFactCapture> Captures = default)
{
    public bool SearchAllSentences { get; init; }

    public ImmutableArray<ImmutableArray<string>> RequiredAnySequences { get; init; } =
        ImmutableArray<ImmutableArray<string>>.Empty;

    public ImmutableArray<ImmutableArray<string>> ForbiddenAnySequences { get; init; } =
        ImmutableArray<ImmutableArray<string>>.Empty;
}

public sealed record SequenceKnowledgeBase(
    ImmutableArray<SequenceTokenRule> Rules,
    ImmutableArray<SequenceTokenEncoding> Encodings = default,
    ImmutableArray<SequenceInputFactRule> InputRules = default,
    ImmutableArray<SequenceInputFactValidation> Validations = default)
{
    public const string StartToken = "<|lattice.start|>";
}
