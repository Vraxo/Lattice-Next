using System.Collections.Immutable;

namespace Lattice.Core.Generation;

public sealed record SequenceSemanticFact(
    string Id,
    ImmutableDictionary<string, string> Values)
{
    public ImmutableDictionary<string, ImmutableArray<string>> Collections { get; init; } =
        ImmutableDictionary<string, ImmutableArray<string>>.Empty.WithComparers(StringComparer.Ordinal);

    public ImmutableDictionary<string, SemanticCollection> SemanticCollections { get; init; } =
        ImmutableDictionary<string, SemanticCollection>.Empty.WithComparers(StringComparer.Ordinal);

    public ImmutableDictionary<string, SemanticExpression> Expressions { get; init; } =
        ImmutableDictionary<string, SemanticExpression>.Empty.WithComparers(StringComparer.Ordinal);

    public ImmutableDictionary<string, ImmutableArray<SemanticExpression>> ExpressionCollections { get; init; } =
        ImmutableDictionary<string, ImmutableArray<SemanticExpression>>.Empty.WithComparers(StringComparer.Ordinal);

    public ImmutableDictionary<string, SemanticExpressionOperator> ComparisonOperators { get; init; } =
        ImmutableDictionary<string, SemanticExpressionOperator>.Empty.WithComparers(StringComparer.Ordinal);

    /// <summary>
    /// Earliest request token position of each captured scalar value. Positions are language-neutral
    /// ordinals into the request token stream, used to keep projected requirements in request order.
    /// </summary>
    public ImmutableDictionary<string, int> ValuePositions { get; init; } =
        ImmutableDictionary<string, int>.Empty.WithComparers(StringComparer.Ordinal);

    /// <summary>
    /// Request token positions aligned item-by-item with <see cref="Collections"/> and
    /// <see cref="ExpressionCollections"/> values.
    /// </summary>
    public ImmutableDictionary<string, ImmutableArray<int>> CollectionPositions { get; init; } =
        ImmutableDictionary<string, ImmutableArray<int>>.Empty.WithComparers(StringComparer.Ordinal);

    /// <summary>
    /// Exclusive sentence boundary for each positioned collection item. Quoted punctuation and embedded
    /// periods do not end sentences. A boundary at the token count denotes the end of the request.
    /// </summary>
    public ImmutableDictionary<string, ImmutableArray<int>> CollectionSentenceEnds { get; init; } =
        ImmutableDictionary<string, ImmutableArray<int>>.Empty.WithComparers(StringComparer.Ordinal);
}

public sealed record SequenceInputFactCapture(
    string FactId,
    string ValueId,
    ImmutableArray<string> StartAfterTokens,
    ImmutableArray<string> StopBeforeTokens,
    string? LiteralValue = null)
{
    public string? CollectionDelimiter { get; init; }

    public string? CollectionItemSeparator { get; init; }

    public ImmutableArray<string> CollectionDelimiters { get; init; } = ImmutableArray<string>.Empty;

    public ImmutableArray<ImmutableArray<string>> StartAfterSequences { get; init; } =
        ImmutableArray<ImmutableArray<string>>.Empty;

    public ImmutableArray<ImmutableArray<string>> StopBeforeSequences { get; init; } =
        ImmutableArray<ImmutableArray<string>>.Empty;

    public string? CollectionFieldDelimiter { get; init; }

    public int? CollectionFieldIndex { get; init; }

    public string? CollectionTemplate { get; init; }

    public SemanticCollectionInputFormat? SemanticCollectionFormat { get; init; }

    public ImmutableArray<string> TrimPrefixTokens { get; init; } = ImmutableArray<string>.Empty;

    public string TrimSuffixCharacters { get; init; } = string.Empty;

    public bool PreserveRawValue { get; init; }

    public string? SemanticExpressionKind { get; init; }

    /// <summary>
    /// When set, a captured value that cannot be materialized as the declared semantic expression is
    /// dropped instead of rejecting the request. Use only for speculative captures.
    /// </summary>
    public bool OptionalExpression { get; init; }

    public int NumericOffset { get; init; }
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
    /// <summary>Required ordered lookahead within this clause, ending at then or sentence punctuation.</summary>
    public ImmutableArray<string> FollowingMatchTokens { get; init; } = ImmutableArray<string>.Empty;

    public bool SearchAllSentences { get; init; }

    public bool RepeatMatches { get; init; }

    public string? SemanticExpressionKind { get; init; }

    public ImmutableArray<ImmutableArray<string>> RequiredAnySequences { get; init; } =
        ImmutableArray<ImmutableArray<string>>.Empty;

    public ImmutableArray<ImmutableArray<string>> RequiredAllSequences { get; init; } =
        ImmutableArray<ImmutableArray<string>>.Empty;

    public ImmutableArray<ImmutableArray<string>> ForbiddenAnySequences { get; init; } =
        ImmutableArray<ImmutableArray<string>>.Empty;
}

/// <summary>
/// The declarative knowledge one generation language contributes: how a request's words become semantic
/// facts, and which fact combinations are contradictory. It carries no rendering rules — a program is
/// realized by the requirement model and the language pack, never by a stored token sequence.
/// </summary>
public sealed record SequenceKnowledgeBase(
    ImmutableArray<SequenceInputFactRule> InputRules = default,
    ImmutableArray<SequenceInputFactValidation> Validations = default);
