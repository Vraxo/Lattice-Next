using System.Collections.Immutable;

namespace Lattice.Core.Generation;

public sealed record SequenceSemanticFact(
    string Id,
    ImmutableDictionary<string, string> Values);

public sealed record SequenceTokenBinding(
    string FactId,
    string ValueId,
    string? Encoding = null);

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
}

public sealed record SequenceInputFactCapture(
    string FactId,
    string ValueId,
    ImmutableArray<string> StartAfterTokens,
    ImmutableArray<string> StopBeforeTokens,
    string? LiteralValue = null);

public sealed record SequenceInputFactRule(
    string Id,
    ImmutableArray<string> MatchTokens,
    string FactId,
    string? ValueId = null,
    string? LiteralValue = null,
    ImmutableArray<SequenceInputFactCapture> Captures = default);

public sealed record SequenceKnowledgeBase(
    ImmutableArray<SequenceTokenRule> Rules,
    ImmutableArray<SequenceTokenEncoding> Encodings = default,
    ImmutableArray<SequenceInputFactRule> InputRules = default)
{
    public const string StartToken = "<|lattice.start|>";
}
