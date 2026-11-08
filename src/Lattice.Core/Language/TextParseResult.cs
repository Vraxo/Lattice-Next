using System.Collections.Immutable;
using Lattice.Core.Semantics;

namespace Lattice.Core.Language;

public sealed record TextParseResult
{
    private TextParseResult(
        ImmutableArray<SemanticUtterance> utterances,
        string? error)
    {
        Utterances = utterances;
        Error = error;
    }

    public ImmutableArray<SemanticUtterance> Utterances { get; }

    public string? Error { get; }

    public bool IsSuccess => Error is null && !Utterances.IsDefaultOrEmpty;

    public static TextParseResult Success(ImmutableArray<SemanticUtterance> utterances) =>
        new(utterances, null);

    public static TextParseResult Failure(string error) =>
        new(ImmutableArray<SemanticUtterance>.Empty, error);
}
